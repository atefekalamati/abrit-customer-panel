using System.Collections.Concurrent;
using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Nextcloud;

/// <summary>
/// No network, no keys: an in-memory Nextcloud. Thread-safe; call counters let tests assert that an account was
/// created exactly once. <see cref="FailNextCalls"/> injects failures for AC-06. Data is lost on restart (the
/// order/provisioning documents in the database remain the source of truth).
/// </summary>
public sealed class MockNextcloudProvisioner(int failNextCalls = 0) : INextcloudProvisioner
{
    public const string InjectedFailureCode = "mock_injected_failure";

    private readonly ConcurrentDictionary<string, ExternalUserStatus> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _groups = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string User, string Group), byte> _memberships = new();
    private int _failNextCalls = failNextCalls;
    private int _createUserCalls;
    private int _setQuotaCalls;
    private int _createGroupCalls;
    private int _disableUserCalls;

    public ProvisionerMode Mode => ProvisionerMode.Mock;

    public int FailNextCalls
    {
        get => Volatile.Read(ref _failNextCalls);
        set => Volatile.Write(ref _failNextCalls, Math.Max(0, value));
    }

    /// <summary>Number of accounts actually created (not "already exists" answers).</summary>
    public int CreateUserCalls => Volatile.Read(ref _createUserCalls);

    public int SetQuotaCalls => Volatile.Read(ref _setQuotaCalls);

    public int CreateGroupCalls => Volatile.Read(ref _createGroupCalls);

    public int UserCount => _users.Count;

    public int GroupCount => _groups.Count;

    public int DisableUserCalls => Volatile.Read(ref _disableUserCalls);

    /// <summary>Current members of <paramref name="groupId"/>, sorted.</summary>
    public IReadOnlyList<string> GroupMembers(string groupId) =>
        [.. _memberships.Keys.Where(k => k.Group == groupId).Select(k => k.User).Order(StringComparer.Ordinal)];

    public Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
        }

        if (cmd.Groups.Any(g => !_groups.ContainsKey(g)))
        {
            return Task.FromResult(ProvisionResult.Error("group_not_found", "Mock: group does not exist."));
        }

        var created = false;
        _users.GetOrAdd(cmd.ExternalUserId, id =>
        {
            created = true;
            return new ExternalUserStatus(id, null, true);
        });

        if (created)
        {
            Interlocked.Increment(ref _createUserCalls);

            // Like OCS: groups given at creation are applied only when the account is new.
            foreach (var group in cmd.Groups)
            {
                _memberships.TryAdd((cmd.ExternalUserId, group), 0);
            }
        }

        return Task.FromResult(ProvisionResult.Ok(alreadyExisted: !created));
    }

    public Task<ProvisionResult> SetQuotaAsync(string externalUserId, long quotaBytes, CancellationToken ct)
    {
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
        }

        if (!_users.TryGetValue(externalUserId, out var user))
        {
            return Task.FromResult(ProvisionResult.Error("user_not_found", "Mock: user does not exist."));
        }

        Interlocked.Increment(ref _setQuotaCalls);
        _users[externalUserId] = user with { QuotaBytes = quotaBytes };
        return Task.FromResult(ProvisionResult.Ok());
    }

    /// <remarks>The mock stores no files, so <c>UsedBytes</c> is always 0. Reads do not consume injected failures.</remarks>
    public Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct) =>
        Task.FromResult(_users.TryGetValue(externalUserId, out var user)
            ? user with
            {
                UsedBytes = 0,
                Groups = [.. _memberships.Keys.Where(k => k.User == externalUserId).Select(k => k.Group).Order(StringComparer.Ordinal)],
            }
            : null);

    public Task<ProvisionResult> EnsureGroupAsync(string groupId, CancellationToken ct)
    {
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
        }

        var created = _groups.TryAdd(groupId, 0);
        if (created)
        {
            Interlocked.Increment(ref _createGroupCalls);
        }

        return Task.FromResult(ProvisionResult.Ok(alreadyExisted: !created));
    }

    public Task<ProvisionResult> AddUserToGroupAsync(string externalUserId, string groupId, CancellationToken ct)
    {
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
        }

        if (!_users.ContainsKey(externalUserId))
        {
            return Task.FromResult(ProvisionResult.Error("user_not_found", "Mock: user does not exist."));
        }

        if (!_groups.ContainsKey(groupId))
        {
            return Task.FromResult(ProvisionResult.Error("group_not_found", "Mock: group does not exist."));
        }

        var added = _memberships.TryAdd((externalUserId, groupId), 0);
        return Task.FromResult(ProvisionResult.Ok(alreadyExisted: !added));
    }

    public Task<ProvisionResult> RemoveUserFromGroupAsync(string externalUserId, string groupId, CancellationToken ct)
    {
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
        }

        _memberships.TryRemove((externalUserId, groupId), out _);
        return Task.FromResult(ProvisionResult.Ok());
    }

    public Task<ProvisionResult> DisableUserAsync(string externalUserId, CancellationToken ct)
    {
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
        }

        if (!_users.TryGetValue(externalUserId, out var user))
        {
            return Task.FromResult(ProvisionResult.Error("user_not_found", "Mock: user does not exist."));
        }

        Interlocked.Increment(ref _disableUserCalls);
        _users[externalUserId] = user with { Enabled = false };
        return Task.FromResult(ProvisionResult.Ok());
    }

    private bool TryConsumeFailure(out ProvisionResult failure)
    {
        while (true)
        {
            var current = Volatile.Read(ref _failNextCalls);
            if (current <= 0)
            {
                failure = ProvisionResult.Ok();
                return false;
            }

            if (Interlocked.CompareExchange(ref _failNextCalls, current - 1, current) == current)
            {
                failure = ProvisionResult.Error(InjectedFailureCode, "Mock: simulated connector failure.");
                return true;
            }
        }
    }
}

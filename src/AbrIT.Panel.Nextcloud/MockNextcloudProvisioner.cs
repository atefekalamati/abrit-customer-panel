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
    private int _failNextCalls = failNextCalls;
    private int _createUserCalls;
    private int _setQuotaCalls;
    private int _createGroupCalls;

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

    public Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        if (TryConsumeFailure(out var failure))
        {
            return Task.FromResult(failure);
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

    public Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct) =>
        Task.FromResult(_users.GetValueOrDefault(externalUserId));

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

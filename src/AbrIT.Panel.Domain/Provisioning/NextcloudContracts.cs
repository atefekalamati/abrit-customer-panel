namespace AbrIT.Panel.Domain.Provisioning;

public enum ProvisionerMode
{
    Mock,
    Real,
}

/// <summary>Create-if-missing request for one Nextcloud account. Never carries a password (DECISIONS A-07).</summary>
public sealed record EnsureUserCommand(
    string ExternalUserId,
    string DisplayName,
    string? Email,
    IReadOnlyList<string> Groups);

/// <summary>Connector outcome. <see cref="ErrorMessage"/> must never contain secrets; callers sanitize again anyway.</summary>
public sealed record ProvisionResult(bool Succeeded, bool AlreadyExisted, string? ErrorCode, string? ErrorMessage)
{
    public static ProvisionResult Ok(bool alreadyExisted = false) => new(true, alreadyExisted, null, null);

    public static ProvisionResult Error(string code, string message) => new(false, false, code, message);
}

public sealed record ExternalUserStatus(string ExternalUserId, long? QuotaBytes, bool Enabled);

/// <summary>Nextcloud account provisioning (AGENTS.md §6). Implementations: Mock (in-memory) and Real (OCS API).</summary>
public interface INextcloudProvisioner
{
    ProvisionerMode Mode { get; }

    Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct);

    Task<ProvisionResult> SetQuotaAsync(string externalUserId, long quotaBytes, CancellationToken ct);

    Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct);

    Task<ProvisionResult> EnsureGroupAsync(string groupId, CancellationToken ct);
}

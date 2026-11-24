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

/// <summary>Account state read from Nextcloud. <see cref="QuotaBytes"/> <c>null</c> = unlimited / unknown.</summary>
public sealed record ExternalUserStatus(
    string ExternalUserId, long? QuotaBytes, bool Enabled, IReadOnlyList<string>? Groups = null, long? UsedBytes = null);

/// <summary>A read failed (as opposed to "account does not exist"). <see cref="ErrorCode"/> is a sanitized code.</summary>
public sealed class NextcloudConnectorException(string errorCode)
    : InvalidOperationException($"Nextcloud call failed ({errorCode}).")
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>Nextcloud account provisioning (AGENTS.md §6). Implementations: Mock (in-memory) and Real (OCS API).</summary>
public interface INextcloudProvisioner
{
    ProvisionerMode Mode { get; }

    Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct);

    Task<ProvisionResult> SetQuotaAsync(string externalUserId, long quotaBytes, CancellationToken ct);

    /// <summary>Returns <c>null</c> when the account does not exist.</summary>
    /// <exception cref="NextcloudConnectorException">Nextcloud could not be read (auth, network, timeout, ...).</exception>
    Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct);

    Task<ProvisionResult> EnsureGroupAsync(string groupId, CancellationToken ct);

    /// <summary>Idempotent: adding an existing member succeeds.</summary>
    Task<ProvisionResult> AddUserToGroupAsync(string externalUserId, string groupId, CancellationToken ct);

    /// <summary>Idempotent: removing a non-member succeeds.</summary>
    Task<ProvisionResult> RemoveUserFromGroupAsync(string externalUserId, string groupId, CancellationToken ct);

    /// <summary>Disables (does not delete) the account, so the member's files stay recoverable by an admin.</summary>
    Task<ProvisionResult> DisableUserAsync(string externalUserId, CancellationToken ct);
}

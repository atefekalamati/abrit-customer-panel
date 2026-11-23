using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Nextcloud;

/// <summary>
/// Placeholder used only when <c>Nextcloud:Mode=Real</c>; the OCS implementation arrives in phase B.
/// Every call throws, so a Real-mode order ends in <c>Failed</c> with a safe error instead of a fake activation.
/// </summary>
public sealed class NotConfiguredRealProvisioner : INextcloudProvisioner
{
    public ProvisionerMode Mode => ProvisionerMode.Real;

    public Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct) => throw NotYet();

    public Task<ProvisionResult> SetQuotaAsync(string externalUserId, long quotaBytes, CancellationToken ct) => throw NotYet();

    public Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct) => throw NotYet();

    public Task<ProvisionResult> EnsureGroupAsync(string groupId, CancellationToken ct) => throw NotYet();

    private static NotImplementedException NotYet() =>
        new("Real Nextcloud provisioning is not implemented yet (phase B).");
}

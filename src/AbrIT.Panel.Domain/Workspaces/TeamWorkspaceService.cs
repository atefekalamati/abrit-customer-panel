using System.Globalization;
using System.Net.Mail;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Domain.Workspaces;

public interface ITeamWorkspaceRepository
{
    Task<TeamWorkspace?> GetByOrderIdAsync(string orderId, CancellationToken ct = default);

    Task SaveAsync(TeamWorkspace workspace, CancellationToken ct = default);
}

/// <summary>Why a workspace action was refused. Mapped to Persian messages by the UI.</summary>
public enum WorkspaceError
{
    /// <summary>Not a Team plan, or the order is not <c>Active</c>.</summary>
    NotAvailable,
    InvalidEmail,
    DuplicateEmail,
    SeatLimitReached,
    MemberNotFound,

    /// <summary>The connector failed; nothing was activated (the error code is sanitized).</summary>
    ConnectorFailed,
}

public sealed class WorkspaceException(WorkspaceError error, string? connectorErrorCode = null)
    : InvalidOperationException($"Workspace action refused: {error}.")
{
    public WorkspaceError Error { get; } = error;

    public string? ConnectorErrorCode { get; } = connectorErrorCode;
}

/// <summary>Owner-only view of a Team workspace (<c>null</c> from the service = 404).</summary>
public sealed record WorkspaceView(Order Order, string GroupId, string OwnerExternalUserId, int SeatLimit, IReadOnlyList<WorkspaceMember> Members)
{
    public int SeatsUsed => 1 + Members.Count(m => m.OccupiesSeat);
}

public interface ITeamWorkspaceService
{
    /// <summary><c>null</c> when the order does not exist, is not the caller's, or is not a Team plan.</summary>
    Task<WorkspaceView?> GetForOwnerAsync(string userId, string orderId, CancellationToken ct = default);

    /// <summary>Adds a member to the caller's own <c>Active</c> Team order. Returns <c>null</c> when not found / not owned.</summary>
    /// <exception cref="WorkspaceException">Refused (see <see cref="WorkspaceException.Error"/>).</exception>
    Task<WorkspaceMember?> AddMemberAsync(string userId, string orderId, string email, CancellationToken ct = default);

    /// <summary>Removes from the group and disables the account; the seat is freed. Idempotent for removed members.</summary>
    /// <exception cref="WorkspaceException">Refused (see <see cref="WorkspaceException.Error"/>).</exception>
    Task<WorkspaceMember?> RemoveMemberAsync(string userId, string orderId, int memberNumber, CancellationToken ct = default);
}

/// <summary>
/// Same idempotency layers as provisioning: everything runs under the per-order lock, there is one workspace document
/// per order, member ids are deterministic and never reused, and the connector calls are create-if-missing.
/// </summary>
public sealed class TeamWorkspaceService(
    IOrderRepository orders,
    IProvisioningRequestRepository requests,
    ITeamWorkspaceRepository workspaces,
    INextcloudProvisioner provisioner,
    ICustomerDirectory customers,
    IOrderLock orderLock,
    IClock clock) : ITeamWorkspaceService
{
    public const int MaxEmailLength = 254;

    public async Task<WorkspaceView?> GetForOwnerAsync(string userId, string orderId, CancellationToken ct = default)
    {
        var order = await GetOwnedTeamOrderAsync(userId, orderId, ct);
        if (order is null)
        {
            return null;
        }

        var workspace = await workspaces.GetByOrderIdAsync(orderId, ct);
        return ToView(order, workspace);
    }

    public async Task<WorkspaceMember?> AddMemberAsync(string userId, string orderId, string email, CancellationToken ct = default)
    {
        await using (await orderLock.AcquireAsync(OrderService.LockKey(orderId), ct))
        {
            var order = await GetOwnedTeamOrderAsync(userId, orderId, ct);
            if (order is null)
            {
                return null;
            }

            // Ownership first, so another customer gets "not found" whatever the input.
            var normalized = NormalizeEmail(email) ?? throw new WorkspaceException(WorkspaceError.InvalidEmail);
            var request = await RequireActiveAsync(order, ct);
            var workspace = await workspaces.GetByOrderIdAsync(orderId, ct) ?? NewWorkspace(order, request);

            var ownerEmail = NormalizeEmail(await customers.GetEmailAsync(order.OwnerUserId, ct) ?? string.Empty);
            var existing = workspace.Members.FirstOrDefault(m => m.Email == normalized && m.Status != WorkspaceMemberStatus.Removed);
            if (normalized == ownerEmail || existing is { Status: WorkspaceMemberStatus.Active })
            {
                throw new WorkspaceException(WorkspaceError.DuplicateEmail);
            }

            if (workspace.SeatsUsed >= workspace.SeatLimit)
            {
                throw new WorkspaceException(WorkspaceError.SeatLimitReached);
            }

            // Reuse a Pending/Failed reservation for the same e-mail: same external id, so a half-done create is completed.
            var member = existing ?? Reserve(workspace, order, normalized);
            member.Status = WorkspaceMemberStatus.Pending;
            await workspaces.SaveAsync(workspace, ct);

            var result = await CallSafelyAsync(() => CreateMemberAsync(member, workspace.GroupId, request.QuotaBytes, ct), ct);
            member.Status = result.Succeeded ? WorkspaceMemberStatus.Active : WorkspaceMemberStatus.Failed;
            member.LastErrorCode = result.Succeeded ? null : ErrorSanitizer.Sanitize(result.ErrorCode ?? "error");
            await workspaces.SaveAsync(workspace, ct);

            return result.Succeeded ? member : throw new WorkspaceException(WorkspaceError.ConnectorFailed, member.LastErrorCode);
        }
    }

    public async Task<WorkspaceMember?> RemoveMemberAsync(string userId, string orderId, int memberNumber, CancellationToken ct = default)
    {
        await using (await orderLock.AcquireAsync(OrderService.LockKey(orderId), ct))
        {
            var order = await GetOwnedTeamOrderAsync(userId, orderId, ct);
            if (order is null)
            {
                return null;
            }

            await RequireActiveAsync(order, ct);
            var workspace = await workspaces.GetByOrderIdAsync(orderId, ct);
            var member = workspace?.Members.FirstOrDefault(m => m.MemberNumber == memberNumber)
                ?? throw new WorkspaceException(WorkspaceError.MemberNotFound);
            if (member.Status == WorkspaceMemberStatus.Removed)
            {
                return member;
            }

            var result = await CallSafelyAsync(() => DeactivateMemberAsync(member.ExternalUserId, workspace!.GroupId, ct), ct);
            if (!result.Succeeded)
            {
                // Member stays Active (still holds the seat) so the UI never claims a removal that did not happen.
                member.LastErrorCode = ErrorSanitizer.Sanitize(result.ErrorCode ?? "error");
                await workspaces.SaveAsync(workspace!, ct);
                throw new WorkspaceException(WorkspaceError.ConnectorFailed, member.LastErrorCode);
            }

            member.Status = WorkspaceMemberStatus.Removed;
            member.RemovedUtc = clock.UtcNow;
            member.LastErrorCode = null;
            await workspaces.SaveAsync(workspace!, ct);
            return member;
        }
    }

    /// <summary>Trimmed, lower-cased address, or <c>null</c> when it is not a single plain e-mail address.</summary>
    public static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxEmailLength || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        if (!MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed || address.DisplayName.Length > 0)
        {
            return null;
        }

        return address.Host.Contains('.', StringComparison.Ordinal) ? trimmed.ToLower(CultureInfo.InvariantCulture) : null;
    }

    private static WorkspaceView ToView(Order order, TeamWorkspace? workspace) => new(
        order,
        workspace?.GroupId ?? ExternalIds.TeamGroupId(order),
        ExternalIds.ExternalUserId(order),
        workspace?.SeatLimit ?? order.PlanSnapshot.MaxUsers,
        workspace?.Members ?? []);

    private async Task<Order?> GetOwnedTeamOrderAsync(string userId, string orderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(orderId))
        {
            return null;
        }

        var order = await orders.GetAsync(orderId, ct);
        return order is not null && order.OwnerUserId == userId && order.PlanSnapshot.MaxUsers > 1 ? order : null;
    }

    private async Task<ProvisioningRequest> RequireActiveAsync(Order order, CancellationToken ct)
    {
        var request = order.Status == OrderStatus.Active ? await requests.GetByOrderIdAsync(order.OrderId, ct) : null;
        return request is { Status: ProvisioningStatus.Succeeded, ExternalGroupId: not null }
            ? request
            : throw new WorkspaceException(WorkspaceError.NotAvailable);
    }

    private TeamWorkspace NewWorkspace(Order order, ProvisioningRequest request) => new()
    {
        OrderId = order.OrderId,
        GroupId = request.ExternalGroupId!,
        SeatLimit = order.PlanSnapshot.MaxUsers,
    };

    private WorkspaceMember Reserve(TeamWorkspace workspace, Order order, string email)
    {
        var member = new WorkspaceMember
        {
            MemberNumber = workspace.NextMemberNumber++,
            Email = email,
            AddedUtc = clock.UtcNow,
        };
        member.ExternalUserId = ExternalIds.MemberExternalUserId(order, member.MemberNumber);
        workspace.Members.Add(member);
        return member;
    }

    private async Task<ProvisionResult> CreateMemberAsync(WorkspaceMember member, string groupId, long quotaBytes, CancellationToken ct)
    {
        var user = await provisioner.EnsureUserAsync(new EnsureUserCommand(member.ExternalUserId, member.Email, member.Email, [groupId]), ct);
        if (!user.Succeeded)
        {
            return user;
        }

        var group = await provisioner.AddUserToGroupAsync(member.ExternalUserId, groupId, ct);
        return group.Succeeded ? await provisioner.SetQuotaAsync(member.ExternalUserId, quotaBytes, ct) : group;
    }

    private async Task<ProvisionResult> DeactivateMemberAsync(string externalUserId, string groupId, CancellationToken ct)
    {
        var group = await provisioner.RemoveUserFromGroupAsync(externalUserId, groupId, ct);
        return group.Succeeded ? await provisioner.DisableUserAsync(externalUserId, ct) : group;
    }

    private static async Task<ProvisionResult> CallSafelyAsync(Func<Task<ProvisionResult>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return ProvisionResult.Error("connector_exception", ex.GetType().Name);
        }
    }
}

using System.Security.Claims;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace AbrIT.Panel.Module.Security;

public static class AbrPermissions
{
    public static readonly Permission ViewAllOrders = new("ViewAllOrders", "View all AbrIT orders");

    public static readonly Permission ApproveTestPayments = new("ApproveTestPayments", "Approve test payments, retry and cancel orders");

    public static readonly Permission ManageProvisioning = new("ManageProvisioning", "Manage provisioning settings (mock failure injection)");

    public static Permission For(OperatorAction action) => action switch
    {
        OperatorAction.ApprovePayment or OperatorAction.RetryProvisioning or OperatorAction.CancelOrder
            or OperatorAction.RefreshExternalStatus => ApproveTestPayments,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}

/// <summary>Only the Administrator stereotype gets these by default; Authenticated gets none (AGENTS.md §7).</summary>
public sealed class PermissionProvider : IPermissionProvider
{
    private static readonly Permission[] All =
        [AbrPermissions.ViewAllOrders, AbrPermissions.ApproveTestPayments, AbrPermissions.ManageProvisioning];

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult<IEnumerable<Permission>>(All);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
        [new PermissionStereotype { Name = OrchardCoreConstants.Roles.Administrator, Permissions = All }];
}

/// <summary>
/// The actor must be the signed-in user of the current request and hold the mapped permission. Service-level
/// defence in depth: controllers check the same permission first.
/// </summary>
public sealed class OrchardOperatorAuthorizer(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    : IOperatorAuthorizer
{
    public async Task<bool> IsAuthorizedAsync(string actorUserId, OperatorAction action, CancellationToken ct = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || principal.FindFirstValue(ClaimTypes.NameIdentifier) != actorUserId)
        {
            return false;
        }

        return await authorizationService.AuthorizeAsync(principal, AbrPermissions.For(action));
    }
}

public sealed class UserManagerCustomerDirectory(UserManager<IUser> userManager) : ICustomerDirectory
{
    public async Task<string?> GetEmailAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await userManager.GetEmailAsync(user);
    }
}

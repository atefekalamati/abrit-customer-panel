using System.Security.Claims;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Domain.Workspaces;
using AbrIT.Panel.Module.Orders;
using AbrIT.Panel.Module.Provisioning;
using AbrIT.Panel.Module.Security;
using AbrIT.Panel.Module.Workspaces;
using AbrIT.Panel.Module.ViewModels;
using AbrIT.Panel.Nextcloud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using YesSql;
using YesSql.Services;

namespace AbrIT.Panel.Module.Controllers;

/// <summary>Every action checks its permission server-side; POSTs require an antiforgery token (AGENTS.md §9).</summary>
[Admin]
public sealed class OrdersAdminController(
    IAuthorizationService authorizationService,
    ISession session,
    IPaymentApprovalService approvals,
    IProvisioningService provisioning,
    IAdminOrderService adminOrders,
    ICustomerDirectory customers,
    UserManager<IUser> userManager,
    ILogger<OrdersAdminController> logger) : Controller
{
    private const int PageSize = 20;
    private const int MaxSearchLength = 100;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? q, int page = 1)
    {
        if (!await authorizationService.AuthorizeAsync(User, AbrPermissions.ViewAllOrders))
        {
            return Forbid();
        }

        OrderStatus? filter = Enum.TryParse<OrderStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        page = Math.Max(1, page);
        var search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (search?.Length > MaxSearchLength)
        {
            search = search[..MaxSearchLength];
        }

        // Search: order number (contains) or the customer's e-mail / user name (exact, resolved to the owner id).
        // Values are captured as locals: YesSql does not translate method calls on captured values.
        var statusText = filter?.ToString();
        var ownerId = search is null ? null : await FindOwnerIdAsync(search);
        var numberPart = search?.ToUpperInvariant();
        var query = session.Query<Order, OrderIndex>();
        if (statusText is not null)
        {
            query = query.Where(x => x.Status == statusText);
        }

        if (numberPart is not null)
        {
            query = ownerId is not null
                ? query.Where(x => x.OrderNumber.Contains(numberPart) || x.OwnerUserId == ownerId)
                : query.Where(x => x.OrderNumber.Contains(numberPart));
        }

        var total = await query.CountAsync();
        var orders = (await query.OrderByDescending(x => x.CreatedUtc).Skip((page - 1) * PageSize).Take(PageSize).ListAsync()).ToList();

        var ids = orders.Select(o => o.OrderId).ToArray();
        var requests = ids.Length == 0
            ? []
            : (await session.Query<ProvisioningRequest, ProvisioningRequestIndex>(x => x.OrderId.IsIn(ids)).ListAsync())
                .GroupBy(r => r.OrderId)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.CreatedUtc).First());
        var teams = ids.Length == 0
            ? []
            : (await session.Query<TeamWorkspace, TeamWorkspaceIndex>(x => x.OrderId.IsIn(ids)).ListAsync())
                .GroupBy(w => w.OrderId)
                .ToDictionary(g => g.Key, g => g.First());
        var emails = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var owner in orders.Select(o => o.OwnerUserId).Distinct(StringComparer.Ordinal))
        {
            emails[owner] = await customers.GetEmailAsync(owner);
        }

        var counts = new Dictionary<OrderStatus, int>();
        foreach (var s in Enum.GetValues<OrderStatus>())
        {
            var text = s.ToString();
            counts[s] = await session.QueryIndex<OrderIndex>(x => x.Status == text).CountAsync();
        }

        return View(new AdminOrdersViewModel(
            [.. orders.Select(o => new AdminOrderRow(
                o, requests.GetValueOrDefault(o.OrderId), teams.GetValueOrDefault(o.OrderId), emails.GetValueOrDefault(o.OwnerUserId)))],
            filter,
            search,
            page,
            Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)),
            total,
            await authorizationService.AuthorizeAsync(User, AbrPermissions.ApproveTestPayments),
            counts,
            PageSize));
    }

    private async Task<string?> FindOwnerIdAsync(string search)
    {
        var user = search.Contains('@', StringComparison.Ordinal)
            ? await userManager.FindByEmailAsync(search)
            : await userManager.FindByNameAsync(search);
        return user is null ? null : await userManager.GetUserIdAsync(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApprovePayment(string orderId) =>
        RunAsync(orderId, "تأیید پرداخت", async adminId => (await approvals.ApproveAsync(orderId, adminId)).Status);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Retry(string orderId) =>
        RunAsync(orderId, "تلاش مجدد", async adminId => (await provisioning.RetryAsync(orderId, adminId)).Order.Status);

    /// <summary>Bonus §12: read the account's state from Nextcloud (Mock: in-memory) and store a snapshot.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RefreshStatus(string orderId) =>
        RunAsync(orderId, "به‌روزرسانی وضعیت از Nextcloud", async adminId =>
        {
            var outcome = await provisioning.RefreshStatusAsync(orderId, adminId);
            if (outcome.Request?.LastStatusSnapshot?.ErrorCode is { } code)
            {
                logger.LogWarning("Status refresh for order {OrderId} failed: {ErrorCode}", orderId, code);
            }

            return outcome.Order.Status;
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string orderId) =>
        RunAsync(orderId, "لغو سفارش", async adminId => (await adminOrders.CancelAsync(orderId, adminId)).Status);

    private async Task<IActionResult> RunAsync(string orderId, string actionLabel, Func<string, Task<OrderStatus>> action)
    {
        if (!await authorizationService.AuthorizeAsync(User, AbrPermissions.ApproveTestPayments))
        {
            return Forbid();
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        try
        {
            var status = await action(adminId);
            TempData["AbrMessage"] = $"{actionLabel}: وضعیت فعلی «{Display.Labels.Status(status)}».";
            TempData["AbrMessageKind"] = status == OrderStatus.Failed ? "error" : "success";
            logger.LogInformation("Admin {AdminId} ran {Action} on order {OrderId}: {Status}", adminId, actionLabel, orderId, status);
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
        catch (OperatorNotAuthorizedException)
        {
            return Forbid();
        }
        catch (InvalidOrderTransitionException ex)
        {
            TempData["AbrMessageKind"] = "error";
            TempData["AbrMessage"] = $"{actionLabel} در وضعیت «{Display.Labels.Status(ex.From)}» مجاز نیست.";
        }
        catch (ProvisioningAttemptsExhaustedException ex)
        {
            TempData["AbrMessageKind"] = "error";
            TempData["AbrMessage"] = $"سقف {ex.MaxAttempts} تلاش برای فعال‌سازی پر شده است.";
        }

        return RedirectToAction(nameof(Index));
    }
}

[Admin]
public sealed class ProvisioningAdminController(
    IAuthorizationService authorizationService,
    NextcloudOptions options,
    MockNextcloudProvisioner mock,
    ISession session,
    ILogger<ProvisioningAdminController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await authorizationService.AuthorizeAsync(User, AbrPermissions.ManageProvisioning))
        {
            return Forbid();
        }

        var last = await session.QueryIndex<ProvisioningRequestIndex>(x => x.LastAttemptUtc != null)
            .OrderByDescending(x => x.LastAttemptUtc)
            .FirstOrDefaultAsync();

        return View(new ProvisioningSettingsViewModel(
            options.Mode,
            options.Mode == ProvisionerMode.Real ? options.BaseUrl : null,
            options.Mode == ProvisionerMode.Real ? options.AdminUser : null,
            options.IsAppPasswordSet,
            ProvisioningRequest.DefaultMaxAttempts,
            last?.LastAttemptUtc,
            options.Mode == ProvisionerMode.Mock ? mock.FailNextCalls : null,
            options.Mode == ProvisionerMode.Mock ? mock.UserCount : null,
            options.Mode == ProvisionerMode.Mock ? mock.GroupCount : null));
    }

    /// <param name="failNext">Number of upcoming connector calls that fail (0 clears the toggle).</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMockFailures(int failNext)
    {
        if (!await authorizationService.AuthorizeAsync(User, AbrPermissions.ManageProvisioning))
        {
            return Forbid();
        }

        if (options.Mode != ProvisionerMode.Mock)
        {
            return BadRequest();
        }

        mock.FailNextCalls = Math.Clamp(failNext, 0, 100);
        logger.LogInformation("Mock connector failure injection set to {FailNextCalls}", mock.FailNextCalls);
        TempData["AbrMessage"] = mock.FailNextCalls == 0
            ? "شبیه‌سازی خطا خاموش شد."
            : $"{Display.Labels.Digits(mock.FailNextCalls)} فراخوانی بعدی با خطا شبیه‌سازی می‌شود.";
        return RedirectToAction(nameof(Index));
    }
}

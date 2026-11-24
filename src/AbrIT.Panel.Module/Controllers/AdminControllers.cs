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
    ILogger<OrdersAdminController> logger) : Controller
{
    private const int PageSize = 20;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        if (!await authorizationService.AuthorizeAsync(User, AbrPermissions.ViewAllOrders))
        {
            return Forbid();
        }

        OrderStatus? filter = Enum.TryParse<OrderStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        page = Math.Max(1, page);

        // Evaluate outside the predicate: YesSql does not translate method calls on captured values.
        var statusText = filter?.ToString();
        var query = statusText is not null
            ? session.Query<Order, OrderIndex>(x => x.Status == statusText)
            : session.Query<Order, OrderIndex>();
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

        return View(new AdminOrdersViewModel(
            [.. orders.Select(o => new AdminOrderRow(o, requests.GetValueOrDefault(o.OrderId), teams.GetValueOrDefault(o.OrderId)))],
            filter,
            page,
            Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)),
            total,
            await authorizationService.AuthorizeAsync(User, AbrPermissions.ApproveTestPayments)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApprovePayment(string orderId) =>
        RunAsync(orderId, "تأیید پرداخت", async adminId => (await approvals.ApproveAsync(orderId, adminId)).Status);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Retry(string orderId) =>
        RunAsync(orderId, "تلاش مجدد", async adminId => (await provisioning.RetryAsync(orderId, adminId)).Order.Status);

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
            TempData["AbrMessage"] = $"{actionLabel} در وضعیت «{Display.Labels.Status(ex.From)}» مجاز نیست.";
        }
        catch (ProvisioningAttemptsExhaustedException ex)
        {
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
    ILogger<ProvisioningAdminController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await authorizationService.AuthorizeAsync(User, AbrPermissions.ManageProvisioning))
        {
            return Forbid();
        }

        return View(new ProvisioningSettingsViewModel(
            options.Mode,
            options.Mode == ProvisionerMode.Real ? options.BaseUrl : null,
            options.IsAppPasswordSet,
            ProvisioningRequest.DefaultMaxAttempts,
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
        TempData["AbrMessage"] = $"{mock.FailNextCalls} فراخوانی بعدی با خطا شبیه‌سازی می‌شود.";
        return RedirectToAction(nameof(Index));
    }
}

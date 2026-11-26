using System.Security.Claims;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Domain.Workspaces;
using AbrIT.Panel.Module.Catalog;
using AbrIT.Panel.Module.ViewModels;
using AbrIT.Panel.Nextcloud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AbrIT.Panel.Module.Controllers;

public sealed class ServicesController(CatalogReader catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // "/" is Orchard's HomeRoute to this action. Anonymous visitors get one canonical landing URL (/services);
        // signed-in users keep "/" as it is.
        if (User.Identity?.IsAuthenticated != true && (Request.Path.Value ?? "/") == "/")
        {
            return LocalRedirect(Url.Content("~/services"));
        }

        return View(await catalog.ListServicesAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Drive()
    {
        var service = await catalog.GetServiceAsync(CatalogReader.DriveServiceKey);
        if (service is null)
        {
            return NotFound();
        }

        return View(new DriveViewModel(service, await catalog.ListPlansAsync(service.ContentItemId)));
    }
}

[Authorize]
public sealed class OrdersController(
    CatalogReader catalog, IOrderService orders, ICustomerDirectory customers, ILogger<OrdersController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> New(string? plan)
    {
        var card = string.IsNullOrEmpty(plan) ? null : await catalog.GetPlanAsync(plan);
        if (card is not { IsOrderable: true })
        {
            return NotFound();
        }

        var service = (await catalog.ListServicesAsync()).FirstOrDefault(s => s.ContentItemId == card.ServiceContentItemId);
        return View(new OrderReviewViewModel(
            card, service?.Title, User.Identity?.Name ?? string.Empty, await customers.GetEmailAsync(User.UserId())));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(New))]
    public async Task<IActionResult> NewPost(string? plan)
    {
        try
        {
            var order = await orders.PlaceOrderAsync(User.UserId(), User.Identity?.Name ?? string.Empty, plan ?? string.Empty);
            logger.LogInformation("Order {OrderId} placed by user {UserId}", order.OrderId, order.OwnerUserId);
            return RedirectToAction(nameof(DashboardController.Order), "Dashboard", new { orderId = order.OrderId });
        }
        catch (PlanNotOrderableException)
        {
            return NotFound();
        }
    }
}

[Authorize]
public sealed class DashboardController(
    IOrderService orders,
    IProvisioningRequestRepository requests,
    ITeamWorkspaceService workspaces,
    NextcloudOptions nextcloud,
    ILogger<DashboardController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(new DashboardViewModel(User.Identity?.Name ?? string.Empty, await ListRowsAsync()));

    /// <summary>«سفارش‌های من»: all of the customer's orders (table on desktop, cards on phones).</summary>
    [HttpGet]
    public async Task<IActionResult> Orders() => View(new MyOrdersViewModel(await ListRowsAsync()));

    private async Task<List<DashboardOrderRow>> ListRowsAsync()
    {
        var rows = new List<DashboardOrderRow>();
        foreach (var order in (await orders.ListForOwnerAsync(User.UserId())).OrderByDescending(o => o.CreatedUtc))
        {
            // Mode is needed only to label Active as Mock (spec §7); other statuses need no lookup.
            var mode = order.Status == OrderStatus.Active ? (await requests.GetByOrderIdAsync(order.OrderId))?.Mode : null;
            rows.Add(new DashboardOrderRow(order, mode));
        }

        return rows;
    }

    [HttpGet]
    public async Task<IActionResult> Order(string orderId)
    {
        // 404 (not 403) for someone else's order: existence is not revealed (DECISIONS A-10). The page says
        // «پیدا نشد» for both cases.
        var order = await orders.GetForOwnerAsync(User.UserId(), orderId);
        if (order is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("OrderNotFound");
        }

        var request = await requests.GetByOrderIdAsync(order.OrderId);
        return View(new CustomerOrderViewModel(
            order,
            request,
            await workspaces.GetForOwnerAsync(User.UserId(), order.OrderId),
            request?.Mode == ProvisionerMode.Real ? nextcloud.BaseUrl : null,
            nextcloud.Mode));
    }

    /// <summary>Team workspace (bonus §12): owner-only, Active Team orders only; anything else is 404.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddMember(string orderId, string? email) =>
        RunWorkspaceAsync(orderId, async userId =>
        {
            var member = await workspaces.AddMemberAsync(userId, orderId, email ?? string.Empty);
            if (member is not null)
            {
                logger.LogInformation("Workspace member {MemberNumber} added to order {OrderId}", member.MemberNumber, orderId);
                TempData["AbrMessage"] = "عضو جدید به فضای تیمی اضافه شد.";
            }

            return member is not null;
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveMember(string orderId, int memberNumber) =>
        RunWorkspaceAsync(orderId, async userId =>
        {
            var member = await workspaces.RemoveMemberAsync(userId, orderId, memberNumber);
            if (member is not null)
            {
                logger.LogInformation("Workspace member {MemberNumber} removed from order {OrderId}", memberNumber, orderId);
                TempData["AbrMessage"] = "عضو از فضای تیمی حذف و حساب او غیرفعال شد.";
            }

            return member is not null;
        });

    private async Task<IActionResult> RunWorkspaceAsync(string orderId, Func<string, Task<bool>> action)
    {
        try
        {
            if (!await action(User.UserId()))
            {
                return NotFound();
            }
        }
        catch (WorkspaceException ex)
        {
            if (ex.Error == WorkspaceError.ConnectorFailed)
            {
                logger.LogWarning("Workspace action on order {OrderId} failed: {ErrorCode}", orderId, ex.ConnectorErrorCode);
            }

            TempData["AbrMessageKind"] = "error";
            TempData["AbrMessage"] = ex.Error switch
            {
                WorkspaceError.NotAvailable => "مدیریت اعضا فقط برای سفارش تیمی فعال امکان‌پذیر است.",
                WorkspaceError.InvalidEmail => "نشانی ایمیل معتبر نیست.",
                WorkspaceError.DuplicateEmail => "این ایمیل قبلاً در این فضای تیمی ثبت شده است.",
                WorkspaceError.SeatLimitReached => "ظرفیت اعضای این پلن تکمیل است.",
                WorkspaceError.MemberNotFound => "عضو موردنظر پیدا نشد.",
                _ => "ارتباط با سرویس درایو ناموفق بود؛ تغییری اعمال نشد. بعداً دوباره تلاش کنید.",
            };
        }

        return RedirectToAction(nameof(Order), new { orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string orderId)
    {
        try
        {
            if (await orders.CancelAsync(User.UserId(), orderId) is null)
            {
                return NotFound();
            }
        }
        catch (InvalidOrderTransitionException)
        {
            TempData["AbrMessage"] = "این سفارش دیگر قابل لغو نیست.";
            TempData["AbrMessageKind"] = "error";
        }

        return RedirectToAction(nameof(Order), new { orderId });
    }
}

internal static class PrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user has no id claim.");
}

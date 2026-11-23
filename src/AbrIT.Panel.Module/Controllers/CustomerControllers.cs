using System.Security.Claims;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Module.Catalog;
using AbrIT.Panel.Module.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AbrIT.Panel.Module.Controllers;

public sealed class ServicesController(CatalogReader catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await catalog.ListServicesAsync());

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
public sealed class OrdersController(CatalogReader catalog, IOrderService orders, ILogger<OrdersController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> New(string? plan)
    {
        var card = string.IsNullOrEmpty(plan) ? null : await catalog.GetPlanAsync(plan);
        return card is { IsOrderable: true } ? View(card) : NotFound();
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
public sealed class DashboardController(IOrderService orders, IProvisioningRequestRepository requests) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await orders.ListForOwnerAsync(User.UserId()));

    [HttpGet]
    public async Task<IActionResult> Order(string orderId)
    {
        // 404 (not 403) for someone else's order: existence is not revealed (DECISIONS A-10).
        var order = await orders.GetForOwnerAsync(User.UserId(), orderId);
        if (order is null)
        {
            return NotFound();
        }

        return View(new CustomerOrderViewModel(order, await requests.GetByOrderIdAsync(order.OrderId)));
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
        }

        return RedirectToAction(nameof(Order), new { orderId });
    }
}

internal static class PrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user has no id claim.");
}

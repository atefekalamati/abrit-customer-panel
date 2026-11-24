using System.Globalization;
using System.Net;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Domain.Workspaces;
using Microsoft.AspNetCore.Html;

namespace AbrIT.Panel.Module.Display;

/// <summary>Persian UI labels and formats (AGENTS.md §4.3, §12). Phase 06 restyles, these stay.</summary>
public static class Labels
{
    private static readonly CultureInfo Fa = CultureInfo.GetCultureInfo("fa-IR");
    private static readonly PersianCalendar Jalali = new();

    public static string Status(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار تأیید پرداخت",
        OrderStatus.Paid => "پرداخت‌شده",
        OrderStatus.Provisioning => "در حال فعال‌سازی",
        OrderStatus.Active => "فعال",
        OrderStatus.Failed => "خطا در فعال‌سازی",
        OrderStatus.Cancelled => "لغوشده",
        _ => status.ToString(),
    };

    /// <summary>In Mock mode Active is never shown as a real active service (spec §7).</summary>
    public static IHtmlContent StatusHtml(OrderStatus status, ProvisionerMode? mode) =>
        status == OrderStatus.Active && mode == ProvisionerMode.Mock
            ? new HtmlString("فعال آزمایشی <bdi>(Mock)</bdi>")
            : new HtmlString(WebUtility.HtmlEncode(Status(status)));

    public static string Price(long toman) => toman.ToString("N0", Fa) + " تومان";

    public static string Date(DateTime? utc)
    {
        if (utc is not { } value)
        {
            return "—";
        }

        var d = value.ToUniversalTime();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Jalali.GetYear(d):D4}/{Jalali.GetMonth(d):D2}/{Jalali.GetDayOfMonth(d):D2} {d:HH:mm} UTC");
    }

    public static string Quota(int megabytes) =>
        megabytes % 1024 == 0 ? $"{megabytes / 1024} گیگابایت" : $"{megabytes} مگابایت";

    public static string Actor(string actorId) => actorId switch
    {
        SystemActors.PaymentCallback => "بازگشت پرداخت آزمایشی",
        SystemActors.Provisioning => "سیستم",
        _ => actorId,
    };

    public static string MemberStatus(WorkspaceMemberStatus status) => status switch
    {
        WorkspaceMemberStatus.Active => "فعال",
        WorkspaceMemberStatus.Removed => "حذف‌شده",
        WorkspaceMemberStatus.Failed => "خطا در افزودن",
        _ => "در حال افزودن",
    };

    public static string Number(int value) => value.ToString(Fa);
}

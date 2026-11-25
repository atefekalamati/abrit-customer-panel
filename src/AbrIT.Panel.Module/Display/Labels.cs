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

    /// <summary>Badge text for Active in Mock mode: «فعال (آزمایشی – Mock)» (DESIGN_PROMPTS); "Mock" is bidi-isolated.</summary>
    public const string ActiveMockHtml = "فعال (آزمایشی – <bdi>Mock</bdi>)";

    /// <summary>In Mock mode Active is never shown as a real active service (spec §7).</summary>
    public static IHtmlContent StatusHtml(OrderStatus status, ProvisionerMode? mode) =>
        status == OrderStatus.Active && mode == ProvisionerMode.Mock
            ? new HtmlString(ActiveMockHtml)
            : new HtmlString(WebUtility.HtmlEncode(Status(status)));

    /// <summary>Badge key for <c>.abr-badge[data-status]</c>: <c>ActiveMock</c> keeps Mock activations visually apart.</summary>
    public static string StatusKey(OrderStatus status, ProvisionerMode? mode) =>
        status == OrderStatus.Active && mode == ProvisionerMode.Mock ? "ActiveMock" : status.ToString();

    /// <summary>«۹۹,۰۰۰ تومان»: Persian digits, «,» grouping (docs/DESIGN_PROMPTS.md).</summary>
    public static string Price(long toman) => Amount(toman) + " تومان";

    public static string Amount(long toman) => ToFa(toman.ToString("N0", CultureInfo.InvariantCulture));

    /// <summary>
    /// Iran Standard Time (UTC+03:30). Iran has had no daylight saving since 2022, so a fixed offset is exact and needs
    /// no tzdata in the container (DECISIONS D-23). Storage stays UTC.
    /// </summary>
    public static readonly TimeSpan TehranOffset = new(3, 30, 0);

    public static DateTime ToTehran(DateTime utc) =>
        DateTime.SpecifyKind((utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc) + TehranOffset, DateTimeKind.Unspecified);

    /// <summary>«۱۴۰۵/۰۹/۰۲ ۱۰:۲۴» (Jalali, Tehran time); «—» when empty.</summary>
    public static string Date(DateTime? utc) => utc is { } value ? Day(value) + " " + Time(value) : "—";

    /// <summary>Jalali date only («۱۴۰۵/۰۹/۰۲», Tehran time) for tables and cards.</summary>
    public static string Day(DateTime utc)
    {
        var d = ToTehran(utc);
        return ToFa(string.Create(
            CultureInfo.InvariantCulture, $"{Jalali.GetYear(d):D4}/{Jalali.GetMonth(d):D2}/{Jalali.GetDayOfMonth(d):D2}"));
    }

    /// <summary>«۱۰:۲۴» (Tehran time).</summary>
    public static string Time(DateTime utc) => ToFa(ToTehran(utc).ToString("HH:mm", CultureInfo.InvariantCulture));

    public static string Quota(long megabytes) =>
        megabytes % 1024 == 0 ? $"{Digits(megabytes / 1024)} گیگابایت" : QuotaGb(megabytes);

    private static string QuotaGb(long megabytes) =>
        megabytes > 1024
            ? Decimal(megabytes / 1024d) + " گیگابایت"
            : $"{Digits(megabytes)} مگابایت";

    /// <summary>Service name for an order line; the plan snapshot only carries the plan (all plans are Drive today).</summary>
    public static string ServiceName(string planKey) =>
        planKey.StartsWith("drive", StringComparison.Ordinal) ? "AbrIT Drive" : string.Empty;

    public static string BillingPeriod(string period) => period switch
    {
        "Monthly" => "ماهانه",
        "Yearly" => "سالانه",
        _ => period,
    };

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

    public static string Number(int value) => Digits(value);

    /// <summary>«۱ از ۵».</summary>
    public static string OutOf(int value, int total) => $"{Digits(value)} از {Digits(total)}";

    public static string Payment(PaymentStatus status) => status == PaymentStatus.ConfirmedSimulated
        ? "تأییدشده (شبیه‌سازی)"
        : "در انتظار تأیید";

    /// <summary>Persian digits (۰–۹) for UI copy; .NET formatting never substitutes native digits.</summary>
    public static string Digits(long value) => ToFa(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Persian decimal: Persian digits and «٫» (U+066B ARABIC DECIMAL SEPARATOR), e.g. 1.5 → «۱٫۵».</summary>
    public static string Decimal(double value, int maxFractionDigits = 1) =>
        ToFa(value.ToString("0." + new string('#', maxFractionDigits), CultureInfo.InvariantCulture))
            .Replace(".", DecimalSeparator, StringComparison.Ordinal);

    public const string DecimalSeparator = "٫";

    public static string ToFa(string latin) =>
        string.Concat(latin.Select(c => c is >= '0' and <= '9' ? (char)('۰' + (c - '0')) : c));
}

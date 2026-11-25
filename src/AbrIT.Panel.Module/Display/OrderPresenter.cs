using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using Microsoft.AspNetCore.Html;

namespace AbrIT.Panel.Module.Display;

/// <summary>
/// Turns an order (+ its provisioning request) into what the order pages show: the 4-step stepper and the status
/// history. Pure and server-side, so customer and admin pages tell the same story.
/// </summary>
public static class OrderPresenter
{
    public static IReadOnlyList<StepItem> Steps(Order order)
    {
        var created = Labels.Date(order.CreatedUtc);
        return order.Status switch
        {
            OrderStatus.PendingPayment =>
            [
                new("ثبت سفارش", StepState.Done, created),
                new("تأیید پرداخت", StepState.Current, "در انتظار تأیید توسط مدیر", "clock"),
                new("فعال‌سازی سرویس", StepState.Upcoming, "پس از تأیید پرداخت"),
                new("آماده‌ی استفاده", StepState.Upcoming, "دسترسی به سرویس پس از فعال‌سازی"),
            ],
            OrderStatus.Paid or OrderStatus.Provisioning =>
            [
                new("ثبت سفارش", StepState.Done, created),
                new("تأیید پرداخت", StepState.Done, Labels.Date(order.PaidUtc)),
                order.Status == OrderStatus.Paid
                    ? new("فعال‌سازی سرویس", StepState.Current, "در صف فعال‌سازی", "clock")
                    : new("فعال‌سازی سرویس", StepState.Current, "در حال ایجاد حساب و تنظیم سهمیه", "refresh"),
                new("آماده‌ی استفاده", StepState.Upcoming, "دسترسی به سرویس پس از فعال‌سازی"),
            ],
            OrderStatus.Active =>
            [
                new("ثبت سفارش", StepState.Done, created),
                new("تأیید پرداخت", StepState.Done, Labels.Date(order.PaidUtc)),
                new("فعال‌سازی سرویس", StepState.Done),
                new("آماده‌ی استفاده", StepState.Done, Labels.Date(order.ActivatedUtc)),
            ],
            OrderStatus.Failed =>
            [
                new("ثبت سفارش", StepState.Done, created),
                new("تأیید پرداخت", StepState.Done, Labels.Date(order.PaidUtc)),
                new("فعال‌سازی سرویس", StepState.Failed, "خطا — در حال پیگیری"),
                new("آماده‌ی استفاده", StepState.Upcoming),
            ],
            _ =>
            [
                new("ثبت سفارش", StepState.Done, created),
                new("تأیید پرداخت", StepState.Skipped, "سفارش لغو شد"),
                new("فعال‌سازی سرویس", StepState.Skipped),
                new("آماده‌ی استفاده", StepState.Skipped),
            ],
        };
    }

    /// <param name="forAdmin">Admin rows name the actor; customer rows explain the step instead.</param>
    public static IReadOnlyList<TimelineEntry> History(Order order, ProvisionerMode? mode, bool forAdmin = false)
    {
        var rows = new List<TimelineEntry>
        {
            new("ثبت سفارش", Labels.Time(order.CreatedUtc), forAdmin ? null : "سفارش شما با موفقیت ثبت شد.", "success",
                Labels.Day(order.CreatedUtc), order.OwnerUserName),
        };

        for (var i = 0; i < order.History.Count; i++)
        {
            var change = order.History[i];
            var last = i == order.History.Count - 1;
            var actor = ActorName(order, change.ActorUserId);
            var time = Labels.Time(change.AtUtc);
            var day = Labels.Day(change.AtUtc);
            rows.Add(change.To switch
            {
                OrderStatus.Paid => new("تأیید پرداخت", time, forAdmin ? null : "پرداخت آزمایشی توسط مدیر تأیید شد.", "success", day, actor),
                OrderStatus.Provisioning => new(
                    change.From == OrderStatus.Failed ? "تلاش مجدد فعال‌سازی" : "شروع فعال‌سازی",
                    time,
                    null,
                    last ? "info" : "success",
                    day,
                    actor),
                OrderStatus.Active => new(
                    mode == ProvisionerMode.Mock ? new HtmlString("فعال شد (آزمایشی – <bdi>Mock</bdi>)") : "فعال شد",
                    time,
                    null,
                    mode == ProvisionerMode.Mock ? "mock" : "success",
                    day,
                    actor),
                OrderStatus.Failed => new(
                    "خطا در فعال‌سازی",
                    time,
                    forAdmin ? change.Note : "فعال‌سازی به دلیل بروز مشکل فنی انجام نشد. تیم فنی در حال بررسی است.",
                    "error",
                    day,
                    actor),
                OrderStatus.Cancelled => new(
                    forAdmin ? "لغو سفارش" : change.ActorUserId == order.OwnerUserId ? "لغو سفارش توسط شما" : "لغو سفارش توسط مدیر",
                    time,
                    null,
                    "neutral",
                    day,
                    actor),
                _ => new(Labels.Status(change.To), time, null, "neutral", day, actor),
            });
        }

        if (order.Status == OrderStatus.PendingPayment)
        {
            rows.Add(new("تأیید پرداخت", "—", "در انتظار.", "pending"));
        }

        return rows;
    }

    private static string ActorName(Order order, string actorId) =>
        actorId == order.OwnerUserId ? order.OwnerUserName
        : actorId is SystemActors.Provisioning or SystemActors.PaymentCallback ? Labels.Actor(actorId)
        : "مدیر";
}

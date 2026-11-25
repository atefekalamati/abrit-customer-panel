using AbrIT.Panel.Module.Display;

namespace AbrIT.Panel.Web.Tests;

public sealed class LabelsTests
{
    [Fact]
    public void Decimal_UsesPersianDigitsAndArabicDecimalSeparator()
    {
        Assert.Equal("۱٫۵", Labels.Decimal(1.5));
        Assert.Equal("۱٫۵ گیگابایت", Labels.Quota(1536));
        Assert.Equal("۱ گیگابایت", Labels.Quota(1024));
        Assert.Equal("۵۰۰ مگابایت", Labels.Quota(500));
    }

    [Fact]
    public void Price_UsesPersianDigitsAndCommaGrouping() => Assert.Equal("۲۹۹,۰۰۰ تومان", Labels.Price(299_000));

    [Fact]
    public void ActiveMock_IsTheAgreedLabel() =>
        Assert.Equal("فعال (آزمایشی – <bdi>Mock</bdi>)", Labels.StatusHtml(Domain.Orders.OrderStatus.Active, Domain.Provisioning.ProvisionerMode.Mock).ToString());

    [Fact]
    public void Date_IsJalali_InTehranTime()
    {
        // 2026-11-23 06:54 UTC = 1405/09/02 10:24 Tehran (UTC+03:30).
        var utc = new DateTime(2026, 11, 23, 6, 54, 0, DateTimeKind.Utc);
        Assert.Equal("۱۴۰۵/۰۹/۰۲ ۱۰:۲۴", Labels.Date(utc));
        Assert.Equal("۱۰:۲۴", Labels.Time(utc));

        // Crosses midnight: 21:00 UTC is already the next Jalali day in Tehran.
        Assert.Equal("۱۴۰۵/۰۹/۰۳", Labels.Day(new DateTime(2026, 11, 23, 21, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void OutOf_UsesPersianDigits() => Assert.Equal("۱ از ۵", Labels.OutOf(1, 5));
}

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
}

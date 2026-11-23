using AbrIT.Panel.Domain.Seeding;

namespace AbrIT.Panel.Domain.Tests;

public class DemoUserSeedingTests
{
    private static readonly DemoUserSpec Spec = new("customer-a", "customer-a@example.test", "x");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_password_skips_silently(string? password) =>
        Assert.Equal(DemoUserAction.Skip, DemoUserSeeding.Decide(Spec with { Password = password }, userExists: false));

    [Fact]
    public void Password_and_missing_user_creates() =>
        Assert.Equal(DemoUserAction.Create, DemoUserSeeding.Decide(Spec, userExists: false));

    [Fact]
    public void Existing_user_is_skipped_so_rerunning_is_idempotent() =>
        Assert.Equal(DemoUserAction.Skip, DemoUserSeeding.Decide(Spec, userExists: true));

    [Fact]
    public void FromSettings_maps_both_customers_with_fixed_emails()
    {
        var settings = new Dictionary<string, string?>
        {
            [DemoUserSeeding.CustomerAPasswordKey] = "a-pass",
        };

        var specs = DemoUserSeeding.FromSettings(k => settings.GetValueOrDefault(k));

        Assert.Collection(
            specs,
            a => { Assert.Equal("customer-a", a.UserName); Assert.Equal("customer-a@example.test", a.Email); Assert.Equal("a-pass", a.Password); },
            b => { Assert.Equal("customer-b", b.UserName); Assert.Equal("customer-b@example.test", b.Email); Assert.Null(b.Password); });
    }

    [Fact]
    public void ToString_never_leaks_the_password() =>
        Assert.DoesNotContain("secret-pw", (Spec with { Password = "secret-pw" }).ToString());
}

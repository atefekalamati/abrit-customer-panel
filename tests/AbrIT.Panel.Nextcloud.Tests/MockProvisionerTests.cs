using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Nextcloud.Tests;

public class MockProvisionerTests
{
    private static EnsureUserCommand Cmd(string id) => new(id, "Customer", null, []);

    [Fact]
    public async Task EnsureUser_creates_once_and_reports_existing_afterwards()
    {
        var mock = new MockNextcloudProvisioner();

        var first = await mock.EnsureUserAsync(Cmd("abrit-ord1405-000001"), default);
        var second = await mock.EnsureUserAsync(Cmd("abrit-ord1405-000001"), default);

        Assert.True(first.Succeeded);
        Assert.False(first.AlreadyExisted);
        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyExisted);
        Assert.Equal(1, mock.CreateUserCalls);
    }

    [Fact]
    public async Task Parallel_EnsureUser_creates_once()
    {
        var mock = new MockNextcloudProvisioner();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => mock.EnsureUserAsync(Cmd("same"), default))));
        Assert.Equal(1, mock.CreateUserCalls);
    }

    [Fact]
    public async Task FailNextCalls_fails_exactly_that_many_calls()
    {
        var mock = new MockNextcloudProvisioner(failNextCalls: 2);

        Assert.False((await mock.EnsureUserAsync(Cmd("u"), default)).Succeeded);
        Assert.False((await mock.EnsureUserAsync(Cmd("u"), default)).Succeeded);
        Assert.True((await mock.EnsureUserAsync(Cmd("u"), default)).Succeeded);
        Assert.Equal(0, mock.FailNextCalls);

        mock.FailNextCalls = -3;
        Assert.Equal(0, mock.FailNextCalls);
    }

    [Fact]
    public async Task SetQuota_stores_bytes_and_requires_existing_user()
    {
        var mock = new MockNextcloudProvisioner();
        Assert.False((await mock.SetQuotaAsync("ghost", 1, default)).Succeeded);

        await mock.EnsureUserAsync(Cmd("u"), default);
        await mock.SetQuotaAsync("u", 1_073_741_824, default);
        Assert.Equal(1_073_741_824, (await mock.GetUserAsync("u", default))!.QuotaBytes);
    }
}

public class NextcloudOptionsTests
{
    [Fact]
    public void Default_is_Mock_and_valid() => Assert.Empty(new NextcloudOptions().Validate());

    [Theory]
    [InlineData(-1)]
    [InlineData(60_001)]
    public void Mock_delay_out_of_range_is_invalid(int delayMs) =>
        Assert.NotEmpty(new NextcloudOptions { Mock = new MockOptions { DelayMs = delayMs } }.Validate());

    [Fact]
    public async Task Mock_delay_is_off_by_default_and_honours_cancellation()
    {
        Assert.Equal(0, new MockNextcloudProvisioner().DelayMs);
        using var cts = new CancellationTokenSource(50);
        var slow = new MockNextcloudProvisioner(delayMs: 10_000);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            slow.EnsureUserAsync(new EnsureUserCommand("abrit-x", "x", null, []), cts.Token));
        Assert.Equal(0, slow.CreateUserCalls);
    }

    [Fact]
    public void Real_with_incomplete_config_fails_fast_without_echoing_secrets()
    {
        var options = new NextcloudOptions { Mode = ProvisionerMode.Real, BaseUrl = "not a url", AppPassword = "top-secret" };

        var ex = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);
        Assert.Contains("BaseUrl", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AdminUser", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Real_with_complete_config_is_valid()
    {
        var options = new NextcloudOptions
        {
            Mode = ProvisionerMode.Real,
            BaseUrl = "http://localhost:8081",
            AdminUser = "admin",
            AppPassword = "x",
        };
        Assert.Empty(options.Validate());
    }
}

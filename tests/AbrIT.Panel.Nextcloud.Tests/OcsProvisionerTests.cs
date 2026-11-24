using System.Net;
using System.Text;
using AbrIT.Panel.Domain.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AbrIT.Panel.Nextcloud.Tests;

/// <summary>Records requests and answers them from a script (no network).</summary>
internal sealed class FakeOcsHandler(Func<HttpRequestMessage, string, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<(HttpMethod Method, string PathAndQuery, string Body, HttpRequestMessage Request)> Calls { get; } = [];

    public static HttpResponseMessage Ocs(int statusCode, string data = "[]", HttpStatusCode http = HttpStatusCode.OK) =>
        new(http)
        {
            Content = new StringContent(
                $$$"""{"ocs":{"meta":{"status":"{{{(statusCode == 100 ? "ok" : "failure")}}}","statuscode":{{{statusCode}}},"message":"m"},"data":{{{data}}}}}""",
                Encoding.UTF8,
                "application/json"),
        };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Calls)
        {
            Calls.Add((request.Method, request.RequestUri!.PathAndQuery, body, request));
        }

        return await respond(request, body).WaitAsync(cancellationToken);
    }
}

/// <summary>Captures log output so tests can prove that no secret is logged.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Lines)
        {
            Lines.Add(formatter(state, exception) + exception);
        }
    }
}

public class OcsProvisionerTests
{
    private const string AdminUser = "ncadmin";
    private const string AppPassword = "Sup3r-Secret-App-Pass";
    private static readonly string Base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AdminUser}:{AppPassword}"));

    private static (OcsNextcloudProvisioner Sut, FakeOcsHandler Handler, ListLogger<OcsNextcloudProvisioner> Log) Create(
        Func<HttpRequestMessage, string, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var handler = new FakeOcsHandler(respond);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cloud.example.test/nc/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
        };
        http.DefaultRequestHeaders.Authorization = OcsNextcloudProvisioner.BasicAuth(AdminUser, AppPassword);
        var log = new ListLogger<OcsNextcloudProvisioner>();
        return (new OcsNextcloudProvisioner(http, log), handler, log);
    }

    private static Func<HttpRequestMessage, string, Task<HttpResponseMessage>> Always(Func<HttpResponseMessage> response) =>
        (_, _) => Task.FromResult(response());

    private static void AssertNoSecret(string? text)
    {
        Assert.DoesNotContain(AppPassword, text ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Base64Credentials, text ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Basic ", text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_call_sends_ocs_headers_basic_auth_and_json_format()
    {
        var (sut, handler, _) = Create(Always(() => FakeOcsHandler.Ocs(100)));

        await sut.SetQuotaAsync("abrit-ord1405-000001", 1, default);
        await sut.EnsureGroupAsync("abrit-team-1405-000001", default);
        await sut.AddUserToGroupAsync("u", "g", default);
        await sut.RemoveUserFromGroupAsync("u", "g", default);
        await sut.DisableUserAsync("u", default);

        Assert.Equal(5, handler.Calls.Count);
        foreach (var (_, pathAndQuery, _, request) in handler.Calls)
        {
            Assert.Equal("true", Assert.Single(request.Headers.GetValues("OCS-APIRequest")));
            Assert.Contains(request.Headers.Accept, h => h.MediaType == "application/json");
            Assert.Contains("format=json", pathAndQuery, StringComparison.Ordinal);
            Assert.StartsWith("/nc/ocs/v1.php/cloud/", pathAndQuery, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Basic_auth_header_reaches_the_server()
    {
        string? auth = null;
        var (sut, _, _) = Create((request, _) =>
        {
            // HttpClient merges DefaultRequestHeaders into the request before the handler runs.
            auth = request.Headers.Authorization?.ToString();
            return Task.FromResult(FakeOcsHandler.Ocs(100));
        });

        await sut.EnsureGroupAsync("g", default);

        Assert.Equal("Basic " + Base64Credentials, auth);
    }

    [Fact]
    public async Task EnsureUser_creates_missing_user_with_email_and_groups_and_no_password()
    {
        var (sut, handler, _) = Create((request, _) => Task.FromResult(
            request.Method == HttpMethod.Get ? FakeOcsHandler.Ocs(404, http: HttpStatusCode.OK) : FakeOcsHandler.Ocs(100)));

        var result = await sut.EnsureUserAsync(new EnsureUserCommand("abrit-ord1405-000001", "Sara", "sara@example.com", ["abrit-team-1405-000001"]), default);

        Assert.True(result.Succeeded);
        Assert.False(result.AlreadyExisted);
        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal("/nc/ocs/v1.php/cloud/users/abrit-ord1405-000001?format=json", handler.Calls[0].PathAndQuery);
        var post = handler.Calls[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/nc/ocs/v1.php/cloud/users?format=json", post.PathAndQuery);
        Assert.Contains("userid=abrit-ord1405-000001", post.Body, StringComparison.Ordinal);
        Assert.Contains("email=sara%40example.com", post.Body, StringComparison.Ordinal);
        Assert.Contains("groups%5B%5D=abrit-team-1405-000001", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("password", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureUser_existing_user_is_success_without_create()
    {
        var (sut, handler, _) = Create(Always(() => FakeOcsHandler.Ocs(100, """{"id":"u"}""")));

        var result = await sut.EnsureUserAsync(new EnsureUserCommand("u", "U", "u@example.com", []), default);

        Assert.True(result.Succeeded);
        Assert.True(result.AlreadyExisted);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task EnsureUser_create_race_102_already_exists_is_success()
    {
        var (sut, _, _) = Create((request, _) => Task.FromResult(
            request.Method == HttpMethod.Get ? FakeOcsHandler.Ocs(404, http: HttpStatusCode.NotFound) : FakeOcsHandler.Ocs(102)));

        var result = await sut.EnsureUserAsync(new EnsureUserCommand("u", "U", "u@example.com", []), default);

        Assert.True(result.Succeeded);
        Assert.True(result.AlreadyExisted);
    }

    [Fact]
    public async Task EnsureUser_without_email_retries_with_random_password_that_is_never_logged_or_returned()
    {
        var (sut, handler, log) = Create((request, body) => Task.FromResult(
            request.Method == HttpMethod.Get ? FakeOcsHandler.Ocs(404)
            : body.Contains("password=", StringComparison.Ordinal) ? FakeOcsHandler.Ocs(100)
            : FakeOcsHandler.Ocs(108)));

        var result = await sut.EnsureUserAsync(new EnsureUserCommand("u", "U", null, []), default);

        Assert.True(result.Succeeded);
        Assert.Equal(3, handler.Calls.Count);
        var password = Uri.UnescapeDataString(handler.Calls[2].Body.Split('&').Single(p => p.StartsWith("password=", StringComparison.Ordinal))[9..]);
        Assert.True(password.Length >= 20);
        Assert.All(log.Lines, line => Assert.DoesNotContain(password, line, StringComparison.Ordinal));
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(103)]
    [InlineData(105)]
    public async Task EnsureUser_ocs_error_maps_to_NC_OCS_code(int ocsCode)
    {
        var (sut, _, _) = Create((request, _) => Task.FromResult(
            request.Method == HttpMethod.Get ? FakeOcsHandler.Ocs(404) : FakeOcsHandler.Ocs(ocsCode)));

        var result = await sut.EnsureUserAsync(new EnsureUserCommand("u", "U", "u@example.com", []), default);

        Assert.False(result.Succeeded);
        Assert.Equal($"NC_OCS_{ocsCode}", result.ErrorCode);
    }

    [Fact]
    public async Task SetQuota_sends_key_quota_and_exact_bytes()
    {
        var (sut, handler, _) = Create(Always(() => FakeOcsHandler.Ocs(100)));

        var result = await sut.SetQuotaAsync("abrit-ord1405-000001", 1_073_741_824, default);

        Assert.True(result.Succeeded);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Put, call.Method);
        Assert.Equal("/nc/ocs/v1.php/cloud/users/abrit-ord1405-000001?format=json", call.PathAndQuery);
        Assert.Equal("key=quota&value=1073741824", call.Body);
    }

    [Fact]
    public async Task Group_calls_use_the_documented_endpoints()
    {
        var (sut, handler, _) = Create(Always(() => FakeOcsHandler.Ocs(100)));

        await sut.EnsureGroupAsync("abrit-team-1405-000001", default);
        await sut.AddUserToGroupAsync("abrit-ord1405-000001-m2", "abrit-team-1405-000001", default);
        await sut.RemoveUserFromGroupAsync("abrit-ord1405-000001-m2", "abrit-team-1405-000001", default);
        await sut.DisableUserAsync("abrit-ord1405-000001-m2", default);

        Assert.Equal((HttpMethod.Post, "/nc/ocs/v1.php/cloud/groups?format=json", "groupid=abrit-team-1405-000001"), (handler.Calls[0].Method, handler.Calls[0].PathAndQuery, handler.Calls[0].Body));
        Assert.Equal((HttpMethod.Post, "/nc/ocs/v1.php/cloud/users/abrit-ord1405-000001-m2/groups?format=json", "groupid=abrit-team-1405-000001"), (handler.Calls[1].Method, handler.Calls[1].PathAndQuery, handler.Calls[1].Body));
        Assert.Equal((HttpMethod.Delete, "/nc/ocs/v1.php/cloud/users/abrit-ord1405-000001-m2/groups?groupid=abrit-team-1405-000001&format=json"), (handler.Calls[2].Method, handler.Calls[2].PathAndQuery));
        Assert.Equal((HttpMethod.Put, "/nc/ocs/v1.php/cloud/users/abrit-ord1405-000001-m2/disable?format=json"), (handler.Calls[3].Method, handler.Calls[3].PathAndQuery));
    }

    [Fact]
    public async Task EnsureGroup_102_is_success()
    {
        var (sut, _, _) = Create(Always(() => FakeOcsHandler.Ocs(102)));

        var result = await sut.EnsureGroupAsync("g", default);

        Assert.True(result.Succeeded);
        Assert.True(result.AlreadyExisted);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "NC_AUTH")]
    [InlineData(HttpStatusCode.Forbidden, "NC_AUTH")]
    [InlineData(HttpStatusCode.InternalServerError, "NC_HTTP_500")]
    [InlineData(HttpStatusCode.BadGateway, "NC_HTTP_502")]
    public async Task Http_errors_map_to_sanitized_codes(HttpStatusCode status, string expected)
    {
        var (sut, _, log) = Create(Always(() => new HttpResponseMessage(status)
        {
            Content = new StringContent($"error for {AdminUser}:{AppPassword} Basic {Base64Credentials}"),
        }));

        var result = await sut.SetQuotaAsync("u", 1, default);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.ErrorCode);
        AssertNoSecret(result.ErrorMessage);
        Assert.All(log.Lines, AssertNoSecret);
    }

    [Fact]
    public async Task Non_json_body_is_bad_response()
    {
        var (sut, _, _) = Create(Always(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>login</html>") }));

        var result = await sut.EnsureGroupAsync("g", default);

        Assert.Equal("NC_BAD_RESPONSE", result.ErrorCode);
    }

    [Fact]
    public async Task Timeout_maps_to_NC_TIMEOUT_without_secrets()
    {
        var (sut, _, log) = Create(
            async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                return FakeOcsHandler.Ocs(100);
            },
            timeout: TimeSpan.FromMilliseconds(100));

        var result = await sut.SetQuotaAsync("u", 1, default);

        Assert.False(result.Succeeded);
        Assert.Equal("NC_TIMEOUT", result.ErrorCode);
        AssertNoSecret(result.ErrorMessage);
        Assert.All(log.Lines, AssertNoSecret);
    }

    [Fact]
    public async Task Network_error_carrying_credentials_is_sanitized()
    {
        var (sut, _, log) = Create((_, _) =>
            throw new HttpRequestException($"Connection refused https://{AdminUser}:{AppPassword}@cloud.example.test Basic {Base64Credentials}"));

        var result = await sut.EnsureGroupAsync("g", default);

        Assert.Equal("NC_NETWORK", result.ErrorCode);
        AssertNoSecret(result.ErrorMessage);
        Assert.All(log.Lines, AssertNoSecret);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_timeout()
    {
        var (sut, _, _) = Create(async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return FakeOcsHandler.Ocs(100);
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SetQuotaAsync("u", 1, cts.Token));
    }

    [Fact]
    public async Task GetUser_parses_quota_enabled_and_groups()
    {
        var (sut, _, _) = Create(Always(() => FakeOcsHandler.Ocs(
            100, """{"id":"u","enabled":false,"quota":{"quota":524288000,"used":1},"groups":["abrit-team-1405-000001"]}""")));

        var user = await sut.GetUserAsync("u", default);

        Assert.NotNull(user);
        Assert.Equal(524_288_000, user.QuotaBytes);
        Assert.False(user.Enabled);
        Assert.Equal(["abrit-team-1405-000001"], user.Groups);
    }

    [Fact]
    public async Task GetUser_missing_returns_null()
    {
        var (sut, _, _) = Create(Always(() => FakeOcsHandler.Ocs(404)));

        Assert.Null(await sut.GetUserAsync("nobody", default));
    }

    [Fact]
    public async Task Registered_pipeline_retries_a_transient_error_exactly_once()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new NextcloudOptions
        {
            Mode = ProvisionerMode.Real,
            BaseUrl = "https://cloud.example.test",
            AdminUser = AdminUser,
            AppPassword = AppPassword,
            TimeoutSeconds = 5,
        });
        services.AddNextcloudConnector();
        services.AddHttpClient(OcsNextcloudProvisioner.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new FakeOcsHandler((_, _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var connector = scope.ServiceProvider.GetRequiredService<INextcloudProvisioner>();
        var result = await connector.EnsureGroupAsync("g", default);

        Assert.IsType<OcsNextcloudProvisioner>(connector);
        Assert.Equal("NC_HTTP_503", result.ErrorCode);
        Assert.Equal(1 + NextcloudServiceCollectionExtensions.TransientRetries, calls);
    }

    [Fact]
    public async Task Mock_is_the_default_registration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new NextcloudOptions());
        services.AddNextcloudConnector();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<MockNextcloudProvisioner>(scope.ServiceProvider.GetRequiredService<INextcloudProvisioner>());
    }
}

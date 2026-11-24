using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AbrIT.Panel.Domain.Provisioning;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace AbrIT.Panel.Nextcloud;

/// <summary>
/// Real connector over the Nextcloud OCS Provisioning API v1 (AGENTS.md §6). Every call sends
/// <c>OCS-APIRequest: true</c>, <c>Accept: application/json</c>, Basic auth (admin user + app password) and
/// <c>format=json</c>. Errors become sanitized codes (<c>NC_TIMEOUT</c>, <c>NC_AUTH</c>, <c>NC_HTTP_{status}</c>,
/// <c>NC_OCS_{code}</c>, <c>NC_NETWORK</c>, <c>NC_BAD_RESPONSE</c>) with fixed messages: no URL, header, credential
/// or server text is ever echoed. Timeout and the single transient retry come from the HttpClient pipeline
/// (<see cref="NextcloudServiceCollectionExtensions.AddNextcloudConnector"/>).
/// </summary>
public sealed class OcsNextcloudProvisioner : INextcloudProvisioner
{
    public const string HttpClientName = "AbrIT.Nextcloud.Ocs";

    // OCS v1 status codes (https://docs.nextcloud.com/server/latest/admin_manual/configuration_user/instruction_set_for_users.html).
    private const int OcsOk = 100;
    private const int OcsAlreadyExists = 102;
    private const int OcsNotFound = 404;
    private const int OcsUserNotFoundOnCreate = 998;
    private const int OcsPasswordOrEmailRequired = 108;

    private readonly HttpClient _http;
    private readonly ILogger<OcsNextcloudProvisioner> _logger;

    public OcsNextcloudProvisioner(HttpClient http, ILogger<OcsNextcloudProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (http.BaseAddress is null)
        {
            throw new InvalidOperationException("The Nextcloud HttpClient has no BaseAddress.");
        }

        _http = http;
        _logger = logger;
    }

    public ProvisionerMode Mode => ProvisionerMode.Real;

    /// <summary>Builds the Basic auth header value. Exposed for registration and tests; never log the result.</summary>
    public static AuthenticationHeaderValue BasicAuth(string user, string appPassword) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{appPassword}")));

    public async Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var existing = await SendAsync(HttpMethod.Get, UserPath(cmd.ExternalUserId), null, ct);
        if (existing.Error is { } readError)
        {
            return readError;
        }

        if (existing.StatusCode == OcsOk)
        {
            return ProvisionResult.Ok(alreadyExisted: true);
        }

        if (existing.StatusCode is not (OcsNotFound or OcsUserNotFoundOnCreate))
        {
            return OcsError(existing.StatusCode, "read user");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("userid", cmd.ExternalUserId),
            new("displayName", cmd.DisplayName),
        };
        if (!string.IsNullOrWhiteSpace(cmd.Email))
        {
            form.Add(new("email", cmd.Email));
        }

        form.AddRange(cmd.Groups.Select(g => new KeyValuePair<string, string>("groups[]", g)));

        var created = await SendAsync(HttpMethod.Post, "ocs/v1.php/cloud/users", form, ct);
        if (created.Error is null && created.StatusCode == OcsPasswordOrEmailRequired)
        {
            // No e-mail → Nextcloud cannot send a set-password link and demands a password. Use a random one that is
            // never stored, logged or shown (DECISIONS A-07); the account owner resets it through the admin.
            form.Add(new("password", RandomPassword()));
            created = await SendAsync(HttpMethod.Post, "ocs/v1.php/cloud/users", form, ct);
        }

        if (created.Error is { } createError)
        {
            return createError;
        }

        return created.StatusCode switch
        {
            OcsOk => ProvisionResult.Ok(),
            OcsAlreadyExists => ProvisionResult.Ok(alreadyExisted: true),
            var code => OcsError(code, "create user"),
        };
    }

    public Task<ProvisionResult> SetQuotaAsync(string externalUserId, long quotaBytes, CancellationToken ct) =>
        SimpleAsync(
            HttpMethod.Put,
            UserPath(externalUserId),
            [new("key", "quota"), new("value", quotaBytes.ToString(CultureInfo.InvariantCulture))],
            "set quota",
            ct);

    public async Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Get, UserPath(externalUserId), null, ct);
        if (response.Error is not null || response.StatusCode != OcsOk || response.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            return null;
        }

        long? quota = null;
        if (data.TryGetProperty("quota", out var q) && q.ValueKind == JsonValueKind.Object
            && q.TryGetProperty("quota", out var qv) && qv.ValueKind == JsonValueKind.Number && qv.TryGetInt64(out var bytes) && bytes >= 0)
        {
            quota = bytes;
        }

        var enabled = !data.TryGetProperty("enabled", out var e) || e.ValueKind != JsonValueKind.False;
        var groups = data.TryGetProperty("groups", out var g) && g.ValueKind == JsonValueKind.Array
            ? g.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];
        return new ExternalUserStatus(externalUserId, quota, enabled, groups);
    }

    public async Task<ProvisionResult> EnsureGroupAsync(string groupId, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Post, "ocs/v1.php/cloud/groups", [new("groupid", groupId)], ct);
        return response.Error ?? response.StatusCode switch
        {
            OcsOk => ProvisionResult.Ok(),
            OcsAlreadyExists => ProvisionResult.Ok(alreadyExisted: true),
            var code => OcsError(code, "create group"),
        };
    }

    public Task<ProvisionResult> AddUserToGroupAsync(string externalUserId, string groupId, CancellationToken ct) =>
        SimpleAsync(HttpMethod.Post, UserPath(externalUserId) + "/groups", [new("groupid", groupId)], "add to group", ct);

    /// <remarks>The group id goes in the query string: OCS reads it from there for DELETE, and DELETE bodies are unreliable.</remarks>
    public Task<ProvisionResult> RemoveUserFromGroupAsync(string externalUserId, string groupId, CancellationToken ct) =>
        SimpleAsync(
            HttpMethod.Delete,
            UserPath(externalUserId) + "/groups?groupid=" + Uri.EscapeDataString(groupId),
            null,
            "remove from group",
            ct);

    public Task<ProvisionResult> DisableUserAsync(string externalUserId, CancellationToken ct) =>
        SimpleAsync(HttpMethod.Put, UserPath(externalUserId) + "/disable", [], "disable user", ct);

    private static string UserPath(string externalUserId) =>
        "ocs/v1.php/cloud/users/" + Uri.EscapeDataString(externalUserId);

    private static ProvisionResult OcsError(int code, string operation) =>
        ProvisionResult.Error($"NC_OCS_{code}", $"Nextcloud rejected '{operation}' (OCS status {code}).");

    private static string RandomPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1!";

    private async Task<ProvisionResult> SimpleAsync(
        HttpMethod method, string path, List<KeyValuePair<string, string>>? form, string operation, CancellationToken ct)
    {
        var response = await SendAsync(method, path, form, ct);
        return response.Error ?? (response.StatusCode == OcsOk ? ProvisionResult.Ok() : OcsError(response.StatusCode, operation));
    }

    private async Task<OcsResponse> SendAsync(
        HttpMethod method, string path, List<KeyValuePair<string, string>>? form, CancellationToken ct)
    {
        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        using var request = new HttpRequestMessage(method, path + separator + "format=json");
        request.Headers.Add("OCS-APIRequest", "true");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        var operation = method.Method + " " + path.Split('?')[0];
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Nextcloud {Operation} rejected the credentials (HTTP {Status})", operation, (int)response.StatusCode);
                return OcsResponse.Failed("NC_AUTH", "Nextcloud rejected the admin credentials.");
            }

            // OCS v1 answers HTTP 200 with the outcome in meta.statuscode; v1 also uses 404 for "user not found".
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Nextcloud {Operation} failed with HTTP {Status}", operation, (int)response.StatusCode);
                var status = (int)response.StatusCode;
                return OcsResponse.Failed($"NC_HTTP_{status}", $"Nextcloud returned HTTP {status}.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return Parse(body, response.StatusCode, operation);
        }
        catch (Exception ex) when (IsTimeout(ex, ct))
        {
            _logger.LogWarning("Nextcloud {Operation} timed out", operation);
            return OcsResponse.Failed("NC_TIMEOUT", "Nextcloud did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            // Only the type/error enum is logged: transport messages can contain the URL.
            _logger.LogWarning("Nextcloud {Operation} network error {Error}", operation, ex.HttpRequestError);
            return OcsResponse.Failed("NC_NETWORK", "Nextcloud could not be reached.");
        }
    }

    private OcsResponse Parse(string body, HttpStatusCode httpStatus, string operation)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("ocs", out var ocs)
                && ocs.TryGetProperty("meta", out var meta)
                && meta.TryGetProperty("statuscode", out var code)
                && code.TryGetInt32(out var statusCode))
            {
                var data = ocs.TryGetProperty("data", out var d) ? d.Clone() : (JsonElement?)null;
                return new OcsResponse(statusCode, data, null);
            }
        }
        catch (JsonException)
        {
        }

        if (httpStatus == HttpStatusCode.NotFound)
        {
            return OcsResponse.Failed("NC_HTTP_404", "Nextcloud returned HTTP 404.");
        }

        _logger.LogWarning("Nextcloud {Operation} returned a response that is not OCS JSON", operation);
        return OcsResponse.Failed("NC_BAD_RESPONSE", "Nextcloud returned an unexpected response.");
    }

    private static bool IsTimeout(Exception ex, CancellationToken ct) =>
        ex is TimeoutRejectedException or TimeoutException
        || (ex is TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested);

    private sealed record OcsResponse(int StatusCode, JsonElement? Data, ProvisionResult? Error)
    {
        public static OcsResponse Failed(string code, string message) => new(0, null, ProvisionResult.Error(code, message));
    }
}

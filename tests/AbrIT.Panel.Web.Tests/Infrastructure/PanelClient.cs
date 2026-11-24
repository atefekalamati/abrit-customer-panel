using System.Net;
using System.Text.RegularExpressions;

namespace AbrIT.Panel.Web.Tests.Infrastructure;

/// <summary>
/// One browser-like user: own cookie jar, real GET/POST through the pipeline, forms submitted with every input the
/// server rendered (hidden fields + antiforgery token) plus the values the test types in.
/// </summary>
public sealed partial class PanelClient(HttpClient http) : IDisposable
{
    public HttpClient Http { get; } = http;

    public async Task<Page> GetAsync(string url, bool followRedirects = true)
    {
        var response = await Http.GetAsync(url);
        var redirects = 0;
        while (followRedirects && IsRedirect(response) && redirects++ < 5)
        {
            response = await Http.GetAsync(response.Headers.Location!);
        }

        return await Page.FromAsync(response);
    }

    /// <summary>GETs <paramref name="pageUrl"/>, fills the form whose action contains <paramref name="actionContains"/>, posts it.</summary>
    /// <param name="fields">Values by exact input name or by name suffix (e.g. "Password" matches "LoginForm.Password").</param>
    public async Task<HttpResponseMessage> SubmitFormAsync(string pageUrl, string actionContains, IDictionary<string, string>? fields = null)
    {
        var page = await GetAsync(pageUrl);
        var form = page.Forms.FirstOrDefault(f => f.Action.Contains(actionContains, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No form with action containing '{actionContains}' on {pageUrl} (status {(int)page.Status}). Forms: "
                + string.Join(", ", page.Forms.Select(f => f.Action)));

        var values = new List<KeyValuePair<string, string>>(form.Inputs);
        foreach (var (key, value) in fields ?? new Dictionary<string, string>())
        {
            var index = values.FindIndex(v => v.Key == key);
            if (index < 0)
            {
                index = values.FindIndex(v => v.Key.EndsWith("." + key, StringComparison.Ordinal));
            }

            if (index >= 0)
            {
                values[index] = new(values[index].Key, value);
            }
            else
            {
                values.Add(new(key, value));
            }
        }

        return await Http.PostAsync(string.IsNullOrEmpty(form.Action) ? pageUrl : form.Action, new FormUrlEncodedContent(values));
    }

    /// <summary>POSTs to an endpoint with only the antiforgery token taken from <paramref name="tokenPageUrl"/>.</summary>
    public async Task<HttpResponseMessage> PostWithTokenAsync(string tokenPageUrl, string postUrl, IDictionary<string, string>? fields = null)
    {
        var page = await GetAsync(tokenPageUrl);
        var token = page.AntiforgeryToken ?? throw new InvalidOperationException($"No antiforgery token on {tokenPageUrl}.");
        var values = new Dictionary<string, string>(fields ?? new Dictionary<string, string>()) { [Page.TokenName] = token };
        return await Http.PostAsync(postUrl, new FormUrlEncodedContent(values));
    }

    public async Task LoginAsync(string userName, string password)
    {
        var response = await SubmitFormAsync("/Login", "Login", new Dictionary<string, string>
        {
            ["UserName"] = userName,
            ["Password"] = password,
        });
        if (!IsRedirect(response))
        {
            throw new InvalidOperationException($"Login of {userName} failed with status {(int)response.StatusCode}.");
        }
    }

    public static bool IsRedirect(HttpResponseMessage response) => (int)response.StatusCode is >= 300 and < 400;

    public void Dispose() => Http.Dispose();

    public sealed record Form(string Action, IReadOnlyList<KeyValuePair<string, string>> Inputs);

    public sealed partial record Page(HttpStatusCode Status, string Url, string Html, IReadOnlyList<Form> Forms)
    {
        public const string TokenName = "__RequestVerificationToken";

        /// <summary>Visible text with HTML entities decoded (Razor encodes Persian text as numeric entities).</summary>
        public string Text => WebUtility.HtmlDecode(Html);

        public string? AntiforgeryToken =>
            Forms.SelectMany(f => f.Inputs).Where(i => i.Key == TokenName).Select(i => i.Value).FirstOrDefault();

        public static async Task<Page> FromAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            var forms = FormRegex().Matches(html).Select(m => new Form(
                WebUtility.HtmlDecode(Attr(m.Groups["attrs"].Value, "action") ?? string.Empty),
                [.. InputRegex().Matches(m.Groups["body"].Value)
                    .Select(i => i.Value)
                    .Where(tag => Attr(tag, "name") is not null)
                    .Where(tag => Attr(tag, "type") is not ("checkbox" or "radio") || tag.Contains(" checked", StringComparison.Ordinal))
                    .Where(tag => Attr(tag, "type") is not ("submit" or "button"))
                    .Select(tag => new KeyValuePair<string, string>(
                        WebUtility.HtmlDecode(Attr(tag, "name")!),
                        WebUtility.HtmlDecode(Attr(tag, "value") ?? string.Empty)))])).ToList();
            return new Page(response.StatusCode, response.RequestMessage?.RequestUri?.PathAndQuery ?? string.Empty, html, forms);
        }

        private static string? Attr(string tag, string name)
        {
            var m = Regex.Match(tag, $@"\s{name}\s*=\s*""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["v"].Value : null;
        }

        [GeneratedRegex(@"<form(?<attrs>[^>]*)>(?<body>.*?)</form>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
        private static partial Regex FormRegex();

        [GeneratedRegex(@"<(input|textarea|select)\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex InputRegex();
    }
}

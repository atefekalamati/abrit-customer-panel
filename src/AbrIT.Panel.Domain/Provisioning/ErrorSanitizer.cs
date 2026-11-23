using System.Text.RegularExpressions;

namespace AbrIT.Panel.Domain.Provisioning;

/// <summary>
/// Makes connector errors safe to store and show to admins: removes credentials in URLs, auth headers and
/// password-like key/value pairs, collapses control characters and truncates (AGENTS.md §9).
/// </summary>
public static partial class ErrorSanitizer
{
    public const int MaxLength = 300;

    public static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error.";
        }

        var text = UrlCredentials().Replace(message, "://***@");
        text = AuthScheme().Replace(text, "$1 ***");
        text = SecretPair().Replace(text, "$1=***");
        text = ControlChars().Replace(text, " ").Trim();

        return text.Length <= MaxLength ? text : text[..MaxLength] + "…";
    }

    [GeneratedRegex(@"://[^/\s:@]+:[^/\s@]+@")]
    private static partial Regex UrlCredentials();

    [GeneratedRegex(@"\b(Basic|Bearer)\s+[A-Za-z0-9+/=._~-]+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthScheme();

    [GeneratedRegex(@"\b(password|passwd|pwd|app[_-]?password|secret|token|apikey|api[_-]key)\s*[=:]\s*[^\s&,;]+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPair();

    [GeneratedRegex(@"[\p{Cc}]+")]
    private static partial Regex ControlChars();
}

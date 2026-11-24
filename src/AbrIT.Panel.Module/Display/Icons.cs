using System.Collections.Frozen;
using System.Globalization;
using Microsoft.AspNetCore.Html;

namespace AbrIT.Panel.Module.Display;

/// <summary>
/// Inline, self-hosted line icons (24×24, stroke = currentColor). Decorative by default (<c>aria-hidden</c>):
/// every icon sits next to visible text or inside a control that carries its own accessible name.
/// </summary>
public static class Icons
{
    private static readonly FrozenDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["home"] = "<path d=\"M3 10.5 12 3l9 7.5\"/><path d=\"M5 9.5V21h14V9.5\"/><path d=\"M10 21v-6h4v6\"/>",
        ["grid"] = "<rect x=\"3.5\" y=\"3.5\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"13.5\" y=\"3.5\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"3.5\" y=\"13.5\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"13.5\" y=\"13.5\" width=\"7\" height=\"7\" rx=\"1.5\"/>",
        ["dashboard"] = "<rect x=\"3.5\" y=\"3.5\" width=\"7\" height=\"9\" rx=\"1.5\"/><rect x=\"13.5\" y=\"3.5\" width=\"7\" height=\"5\" rx=\"1.5\"/><rect x=\"13.5\" y=\"11.5\" width=\"7\" height=\"9\" rx=\"1.5\"/><rect x=\"3.5\" y=\"15.5\" width=\"7\" height=\"5\" rx=\"1.5\"/>",
        ["cloud"] = "<path d=\"M7 18.5h10.5a4.5 4.5 0 0 0 .7-8.95A6 6 0 0 0 6.6 10.2 4.2 4.2 0 0 0 7 18.5Z\"/>",
        ["cart"] = "<circle cx=\"9\" cy=\"20\" r=\"1.4\"/><circle cx=\"18\" cy=\"20\" r=\"1.4\"/><path d=\"M2.5 3.5h2.8l2.5 11.6a1.6 1.6 0 0 0 1.6 1.3h8.6a1.6 1.6 0 0 0 1.6-1.2L21 7.5H6.1\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21a8 8 0 0 1 16 0\"/>",
        ["user-plus"] = "<circle cx=\"9.5\" cy=\"8\" r=\"4\"/><path d=\"M2.5 21a7 7 0 0 1 14 0\"/><path d=\"M19.5 8v6M16.5 11h6\"/>",
        ["users"] = "<circle cx=\"9\" cy=\"8\" r=\"3.5\"/><path d=\"M2.5 20a6.5 6.5 0 0 1 13 0\"/><path d=\"M16 4.6a3.5 3.5 0 0 1 0 6.8\"/><path d=\"M18 14.2a6.5 6.5 0 0 1 3.5 5.8\"/>",
        ["headset"] = "<path d=\"M4 14v-2a8 8 0 0 1 16 0v2\"/><rect x=\"2.5\" y=\"14\" width=\"4.5\" height=\"6.5\" rx=\"1.5\"/><rect x=\"17\" y=\"14\" width=\"4.5\" height=\"6.5\" rx=\"1.5\"/>",
        ["external"] = "<path d=\"M14 4h6v6\"/><path d=\"M20 4l-9 9\"/><path d=\"M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5\"/>",
        ["login"] = "<path d=\"M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4\"/><path d=\"M10 16l4-4-4-4\"/><path d=\"M14 12H3\"/>",
        ["logout"] = "<path d=\"M10 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h4\"/><path d=\"M15 16l-4-4 4-4\"/><path d=\"M11 12h10\"/>",
        ["lock"] = "<rect x=\"4.5\" y=\"10.5\" width=\"15\" height=\"10\" rx=\"2\"/><path d=\"M8 10.5V7a4 4 0 0 1 8 0v3.5\"/><path d=\"M12 14.5v2.5\"/>",
        ["eye"] = "<path d=\"M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["eye-off"] = "<path d=\"M3 3l18 18\"/><path d=\"M10.6 5.1A10 10 0 0 1 12 5c6.4 0 10 7 10 7a17.6 17.6 0 0 1-3.2 4.1\"/><path d=\"M6.6 6.6C3.9 8.4 2 12 2 12s3.6 7 10 7a9.7 9.7 0 0 0 5.4-1.6\"/><path d=\"M9.9 9.9a3 3 0 0 0 4.2 4.2\"/>",
        ["mail"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"M3.5 6.5 12 13l8.5-6.5\"/>",
        ["menu"] = "<path d=\"M4 6.5h16M4 12h16M4 17.5h16\"/>",
        ["close"] = "<path d=\"M6 6l12 12M18 6 6 18\"/>",
        ["chevron-down"] = "<path d=\"M6 9l6 6 6-6\"/>",
        ["chevron-left"] = "<path d=\"M15 18l-6-6 6-6\"/>",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 11v5.5\"/><path d=\"M12 7.6h.01\"/>",
        ["alert"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7.5v5.5\"/><path d=\"M12 16.4h.01\"/>",
        ["check"] = "<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>",
        ["check-circle"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M8 12.5l3 3 5-6\"/>",
        ["circle"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/>",
        ["shield"] = "<path d=\"M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6l8-3Z\"/><path d=\"M8.5 12l2.5 2.5 4.5-5\"/>",
        ["zap"] = "<path d=\"M13 2.5 4.5 14h7l-1 7.5L19 10h-7l1-7.5Z\"/>",
        ["send"] = "<path d=\"M21 3 10 14\"/><path d=\"M21 3l-6.5 18-4.5-7-7-4.5L21 3Z\"/>",
        ["key"] = "<circle cx=\"8\" cy=\"15\" r=\"4.5\"/><path d=\"M11.2 11.8 20 3\"/><path d=\"M16.5 6.5l3 3\"/>",
        ["flask"] = "<path d=\"M9 3h6\"/><path d=\"M10 3v6.2l-5.4 9.3A1.7 1.7 0 0 0 6 21h12a1.7 1.7 0 0 0 1.4-2.5L14 9.2V3\"/><path d=\"M7.4 15h9.2\"/>",
        ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3.2 2\"/>",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool Exists(string name) => Paths.ContainsKey(name);

    public static IHtmlContent Svg(string name, int size = 20, string? cssClass = null)
    {
        var body = Paths.TryGetValue(name, out var paths) ? paths : Paths["circle"];
        var cls = string.IsNullOrEmpty(cssClass) ? "abr-ico" : "abr-ico " + cssClass;
        return new HtmlString(string.Create(
            CultureInfo.InvariantCulture,
            $"<svg class=\"{cls}\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">{body}</svg>"));
    }
}

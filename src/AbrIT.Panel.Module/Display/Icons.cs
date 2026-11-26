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
        ["login"] = "<g transform=\"matrix(-1 0 0 1 24 0)\"><path d=\"M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4\"/><path d=\"M10 16l4-4-4-4\"/><path d=\"M14 12H3\"/></g>", // RTL enter: mirrored, arrow goes into the door leftwards
        ["logout"] = "<path d=\"M14 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4\"/><path d=\"M8 16l-4-4 4-4\"/><path d=\"M4 12h11\"/>", // RTL exit: arrow leaves the door leftwards
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
        ["star"] = "<path d=\"M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8L3.5 9.7l5.9-.9Z\"/>",
        ["layers"] = "<path d=\"M12 3 3 7.5l9 4.5 9-4.5Z\"/><path d=\"M3 12l9 4.5 9-4.5\"/><path d=\"M3 16.5 12 21l9-4.5\"/>",
        ["pointer"] = "<path d=\"M9 9l11 4-4.8 1.7L13.5 20Z\"/><path d=\"M5 3.5 6 6M3.5 8l2.5.6M9.5 2.5 9 5.5\"/>",
        ["clipboard"] = "<rect x=\"5\" y=\"4.5\" width=\"14\" height=\"16.5\" rx=\"2\"/><path d=\"M9 4.5V3h6v1.5\"/><path d=\"M8.5 10h7M8.5 13.5h7M8.5 17h4\"/>",
        ["card"] = "<rect x=\"3\" y=\"5.5\" width=\"18\" height=\"13\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/>",
        ["cloud-up"] = "<path d=\"M7 18.5H6.5a4.5 4.5 0 0 1-.4-9A6 6 0 0 1 17.6 9a4.5 4.5 0 0 1 0 9.5H17\"/><path d=\"M12 20.5V12M8.8 15 12 11.8 15.2 15\"/>",
        ["rocket"] = "<path d=\"M5 15c-1.5 1.3-2 4-2 4s2.7-.5 4-2\"/><path d=\"M9 13.5 10.5 15c4.5-1.5 8-5.5 9-11.5C13.5 4.5 9.5 8 8 12.5Z\"/><path d=\"M8.5 11H5l2.5-3h4M13 15.5V19l3-2.5v-4\"/>",
        ["gear"] = "<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1\"/>",
        ["calendar"] = "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15.5\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["server"] = "<rect x=\"3.5\" y=\"4\" width=\"17\" height=\"7\" rx=\"2\"/><rect x=\"3.5\" y=\"13\" width=\"17\" height=\"7\" rx=\"2\"/><path d=\"M7.5 7.5h.01M7.5 16.5h.01\"/>",
        ["file"] = "<path d=\"M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z\"/><path d=\"M14 3v5h5M9 13h6M9 17h4\"/>",
        ["database"] = "<ellipse cx=\"12\" cy=\"6\" rx=\"7.5\" ry=\"3\"/><path d=\"M4.5 6v12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3V6\"/><path d=\"M4.5 12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3\"/>",
        ["refresh"] = "<path d=\"M20 11a8 8 0 0 0-14.6-4.5L4 8\"/><path d=\"M4 3.5V8h4.5\"/><path d=\"M4 13a8 8 0 0 0 14.6 4.5L20 16\"/><path d=\"M20 20.5V16h-4.5\"/>",
        ["copy"] = "<rect x=\"8.5\" y=\"8.5\" width=\"12\" height=\"12\" rx=\"2\"/><path d=\"M15.5 8.5V5.5a2 2 0 0 0-2-2h-8a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h3\"/>",
        ["trash"] = "<path d=\"M4 7h16\"/><path d=\"M9.5 7V4.5h5V7\"/><path d=\"M6 7l1 13a1.5 1.5 0 0 0 1.5 1.4h7A1.5 1.5 0 0 0 17 20l1-13\"/><path d=\"M10 11v6M14 11v6\"/>",
        ["ban"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M5.7 5.7l12.6 12.6\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4.5 4.5\"/>",
        ["warning"] = "<path d=\"M12 3.5 2.5 20h19Z\"/><path d=\"M12 10v4.5\"/><path d=\"M12 17.4h.01\"/>",
        ["save"] = "<path d=\"M5 3.5h11l3.5 3.5v12a1.5 1.5 0 0 1-1.5 1.5H5A1.5 1.5 0 0 1 3.5 19V5A1.5 1.5 0 0 1 5 3.5Z\"/><path d=\"M7.5 3.5v5h8v-5\"/><rect x=\"7\" y=\"13\" width=\"10\" height=\"7.5\" rx=\"1\"/>",
        ["book"] = "<path d=\"M12 6.5C10 5 7 4.5 3.5 5v13.5c3.5-.5 6.5 0 8.5 1.5 2-1.5 5-2 8.5-1.5V5C17 4.5 14 5 12 6.5Z\"/><path d=\"M12 6.5V20\"/>",
        ["dots"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M8 12h.01M12 12h.01M16 12h.01\"/>",
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

namespace AbrIT.Panel.Module.Display;

/// <summary>Breadcrumb entry for the <c>AbrPageHeader</c> shape; the last entry is the current page (no URL).</summary>
public sealed record Crumb(string Text, string? Url = null);

public enum StepState
{
    Upcoming,
    Current,
    Done,
    Failed,

    /// <summary>Never reached (order cancelled): faded and struck through.</summary>
    Skipped,
}

/// <summary>
/// One step of the <c>AbrStepper</c> shape (horizontal on desktop, vertical on phones). <paramref name="Icon"/> replaces
/// the step number on the current step (e.g. clock while waiting, refresh while provisioning).
/// </summary>
public sealed record StepItem(string Label, StepState State, string? Note = null, string? Icon = null);

/// <summary>
/// One row of the <c>AbrTimeline</c> shape. <paramref name="Tone"/>: success | error | info | warning | mock | neutral |
/// pending (not reached yet). <paramref name="Meta"/> is the time (or «—»), <paramref name="Day"/> the Jalali date.
/// </summary>
public sealed record TimelineEntry(object Title, string Meta, string? Note = null, string Tone = "neutral", string? Day = null, string? Actor = null);

/// <summary>One card of the numbered «چطور کار می‌کند؟» flow (<c>Shared/_Flow.cshtml</c>).</summary>
public sealed record FlowStep(string Icon, string Title, string? Note = null);

/// <summary>Model of <c>Shared/_Flow.cshtml</c>: a titled container of numbered steps with arrows between them.</summary>
public sealed record FlowModel(string Title, IReadOnlyList<FlowStep> Steps, string? Description = null, string Icon = "layers", string? Art = null);

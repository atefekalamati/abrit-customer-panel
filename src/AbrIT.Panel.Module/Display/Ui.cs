namespace AbrIT.Panel.Module.Display;

/// <summary>Breadcrumb entry for the <c>AbrPageHeader</c> shape; the last entry is the current page (no URL).</summary>
public sealed record Crumb(string Text, string? Url = null);

public enum StepState
{
    Upcoming,
    Current,
    Done,
    Failed,
}

/// <summary>One step of the <c>AbrStepper</c> shape (horizontal on desktop, vertical on phones).</summary>
public sealed record StepItem(string Label, StepState State, string? Note = null);

/// <summary>One entry of the <c>AbrTimeline</c> shape. <paramref name="Tone"/>: success | error | info | warning | mock | neutral.</summary>
public sealed record TimelineEntry(string Title, string Meta, string? Note = null, string Tone = "neutral");

/// <summary>One card of the numbered «چطور کار می‌کند؟» flow (<c>Shared/_Flow.cshtml</c>).</summary>
public sealed record FlowStep(string Icon, string Title, string? Note = null);

/// <summary>Model of <c>Shared/_Flow.cshtml</c>: a titled container of numbered steps with arrows between them.</summary>
public sealed record FlowModel(string Title, IReadOnlyList<FlowStep> Steps, string? Description = null, string Icon = "layers", string? Art = null);

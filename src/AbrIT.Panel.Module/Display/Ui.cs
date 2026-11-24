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

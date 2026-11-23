namespace AbrIT.Panel.Module;

/// <summary>Bound from <c>MockPayment</c> (env <c>MockPayment__WebhookSecret</c>). Empty secret disables the callback.</summary>
public sealed class MockPaymentOptions
{
    public const string SectionName = "MockPayment";

    public string? WebhookSecret { get; set; }

    public bool IsEnabled => !string.IsNullOrEmpty(WebhookSecret);
}

using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Nextcloud;

/// <summary>Bound from the <c>Nextcloud</c> configuration section (env vars <c>Nextcloud__*</c>, never committed).</summary>
public sealed class NextcloudOptions
{
    public const string SectionName = "Nextcloud";

    public ProvisionerMode Mode { get; set; } = ProvisionerMode.Mock;

    public string? BaseUrl { get; set; }

    public string? AdminUser { get; set; }

    /// <summary>Secret. Only its presence is ever shown ("set" / "not set").</summary>
    public string? AppPassword { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    public MockOptions Mock { get; set; } = new();

    public bool IsAppPasswordSet => !string.IsNullOrEmpty(AppPassword);

    /// <summary>Configuration problems that must stop the app at startup. Never includes secret values.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (TimeoutSeconds is < 1 or > 300)
        {
            errors.Add("Nextcloud:TimeoutSeconds must be between 1 and 300.");
        }

        if (Mock.FailNextCalls < 0)
        {
            errors.Add("Nextcloud:Mock:FailNextCalls must not be negative.");
        }

        if (Mock.DelayMs is < 0 or > MockOptions.MaxDelayMs)
        {
            errors.Add($"Nextcloud:Mock:DelayMs must be between 0 and {MockOptions.MaxDelayMs}.");
        }

        if (Mode == ProvisionerMode.Real)
        {
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                errors.Add("Nextcloud:BaseUrl must be an absolute http(s) URL when Nextcloud:Mode is Real.");
            }

            if (string.IsNullOrWhiteSpace(AdminUser))
            {
                errors.Add("Nextcloud:AdminUser is required when Nextcloud:Mode is Real.");
            }

            if (string.IsNullOrWhiteSpace(AppPassword))
            {
                errors.Add("Nextcloud:AppPassword is required when Nextcloud:Mode is Real.");
            }
        }

        return errors;
    }

    /// <exception cref="InvalidOperationException">Configuration is incomplete.</exception>
    public void ThrowIfInvalid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid Nextcloud configuration: " + string.Join(" ", errors));
        }
    }
}

public sealed class MockOptions
{
    /// <summary>Initial number of connector calls that fail (AC-06); adjustable at runtime from the admin page.</summary>
    public int FailNextCalls { get; set; }

    public const int MaxDelayMs = 60_000;

    /// <summary>
    /// Dev/demo only: artificial latency (ms) before the Mock creates an account, so the «در حال فعال‌سازی» state can
    /// be seen and captured. Default 0; tests never set it.
    /// </summary>
    public int DelayMs { get; set; }
}

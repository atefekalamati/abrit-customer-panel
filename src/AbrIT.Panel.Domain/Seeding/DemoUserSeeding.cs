namespace AbrIT.Panel.Domain.Seeding;

/// <summary>A demo customer to seed. <see cref="Password"/> comes from the environment and must never be logged.</summary>
public sealed record DemoUserSpec(string UserName, string Email, string? Password)
{
    public override string ToString() => $"{UserName} <{Email}>"; // never includes the password
}

public enum DemoUserAction
{
    /// <summary>No password configured, or the user already exists: do nothing, silently.</summary>
    Skip,
    Create,
}

public static class DemoUserSeeding
{
    public const string CustomerAPasswordKey = "DEMO_CUSTOMER_A_PASSWORD";
    public const string CustomerBPasswordKey = "DEMO_CUSTOMER_B_PASSWORD";

    public static DemoUserAction Decide(DemoUserSpec spec, bool userExists)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Password))
        {
            return DemoUserAction.Skip;
        }

        return userExists ? DemoUserAction.Skip : DemoUserAction.Create;
    }

    /// <summary>The two fixed demo customers; passwords are read through <paramref name="getSetting"/>.</summary>
    public static IReadOnlyList<DemoUserSpec> FromSettings(Func<string, string?> getSetting)
    {
        ArgumentNullException.ThrowIfNull(getSetting);
        return
        [
            new("customer-a", "customer-a@example.test", getSetting(CustomerAPasswordKey)),
            new("customer-b", "customer-b@example.test", getSetting(CustomerBPasswordKey)),
        ];
    }
}

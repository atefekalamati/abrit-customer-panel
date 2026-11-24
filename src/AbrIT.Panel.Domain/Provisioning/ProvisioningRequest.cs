using System.Globalization;
using AbrIT.Panel.Domain.Orders;

namespace AbrIT.Panel.Domain.Provisioning;

public enum ProvisioningStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed,
}

/// <summary>Exactly one per order (created inside the per-order lock). Persisted as a YesSql document.</summary>
public sealed class ProvisioningRequest
{
    public const int DefaultMaxAttempts = 5;

    /// <summary>
    /// Storage document id (YesSql sets it on save/load). Lets an instance that is no longer tracked by the session
    /// (e.g. after a commit) update its own document instead of inserting a copy (DECISIONS D-12).
    /// </summary>
    public long Id { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public ProvisionerMode Mode { get; set; }

    public string ExternalUserId { get; set; } = string.Empty;

    public string? ExternalGroupId { get; set; }

    public long QuotaBytes { get; set; }

    public ProvisioningStatus Status { get; set; }

    public int Attempts { get; set; }

    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastAttemptUtc { get; set; }

    public DateTime? SucceededUtc { get; set; }

    public string? LastErrorCode { get; set; }

    /// <summary>Always passed through <see cref="ErrorSanitizer"/> before being stored.</summary>
    public string? LastErrorMessage { get; set; }

    public bool CanRetry => Status == ProvisioningStatus.Failed && Attempts < MaxAttempts;
}

/// <summary>Deterministic external identifiers (DECISIONS A-06).</summary>
public static class ExternalIds
{
    public const long BytesPerMegabyte = 1024L * 1024L;

    /// <summary>
    /// <c>ABR-1405-000124</c> → <c>abrit-ord1405-000124</c>. The order number is unique and immutable, and the Jalali
    /// year is kept because the sequence restarts every year (A-13). Falls back to the order id for legacy orders.
    /// </summary>
    public static string ExternalUserId(Order order)
    {
        var number = order.OrderNumber;
        if (number.StartsWith("ABR-", StringComparison.Ordinal) && number.Length > 4)
        {
            return "abrit-ord" + number[4..].ToLower(CultureInfo.InvariantCulture);
        }

        return "abrit-" + order.OrderId.ToLower(CultureInfo.InvariantCulture);
    }

    public static string TeamGroupId(string externalUserId) => externalUserId + "-team";

    public static long QuotaBytes(PlanSnapshot plan) => plan.QuotaPerUserMB * BytesPerMegabyte;
}

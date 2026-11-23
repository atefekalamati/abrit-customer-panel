using System.Globalization;

namespace AbrIT.Panel.Domain.Orders;

/// <summary>
/// <c>ABR-{Jalali year}-{6-digit sequence}</c>; the sequence restarts every Jalali year (DECISIONS A-13).
/// Uniqueness comes from incrementing under a per-year lock; the store commits before the lock is released.
/// </summary>
public sealed class SequentialOrderNumberGenerator(ISequenceStore store, IOrderLock orderLock, IClock clock)
    : IOrderNumberGenerator
{
    private static readonly PersianCalendar Calendar = new();

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var year = Calendar.GetYear(clock.UtcNow);
        var key = $"order-number:{year}";

        await using (await orderLock.AcquireAsync(key, ct))
        {
            var sequence = await store.IncrementAsync(key, ct);
            return string.Create(CultureInfo.InvariantCulture, $"ABR-{year}-{sequence:D6}");
        }
    }
}

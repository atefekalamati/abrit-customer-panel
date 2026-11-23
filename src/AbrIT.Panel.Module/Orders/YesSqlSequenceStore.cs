using AbrIT.Panel.Domain.Orders;
using YesSql;

namespace AbrIT.Panel.Module.Orders;

/// <summary>One small document per counter key (e.g. one per Jalali year).</summary>
public sealed class OrderSequence
{
    public string Key { get; set; } = string.Empty;

    public long Value { get; set; }
}

/// <summary>
/// Increments a counter and commits immediately (<see cref="ISession.SaveChangesAsync"/>), so the new value is
/// durable before the caller releases its lock and the next request reads it (DECISIONS A-13, D-11).
/// Called before anything else is written in the request (order numbering precedes the order save).
/// </summary>
public sealed class YesSqlSequenceStore(ISession session) : ISequenceStore
{
    public async Task<long> IncrementAsync(string key, CancellationToken ct = default)
    {
        // Few documents (one per year): no index needed.
        var all = await session.Query<OrderSequence>().ListAsync(ct);
        var sequence = all.FirstOrDefault(s => s.Key == key) ?? new OrderSequence { Key = key };

        sequence.Value++;
        await session.SaveAsync(sequence, cancellationToken: ct);
        await session.SaveChangesAsync(ct);
        return sequence.Value;
    }
}

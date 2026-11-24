using AbrIT.Panel.Domain.Orders;

namespace AbrIT.Panel.Domain.Tests.Orders;

public sealed class OrderNumberTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 11, 23, 9, 30, 0, DateTimeKind.Utc));

    private SequentialOrderNumberGenerator CreateGenerator() =>
        new(new RacySequenceStore(), new SemaphoreOrderLock(), _clock);

    [Fact]
    public async Task Format_is_ABR_jalali_year_and_six_digit_sequence()
    {
        var generator = CreateGenerator();

        Assert.Equal("ABR-1405-000001", await generator.NextAsync());
        Assert.Equal("ABR-1405-000002", await generator.NextAsync());
    }

    [Fact]
    public async Task Sequence_restarts_in_a_new_jalali_year()
    {
        var generator = CreateGenerator();
        await generator.NextAsync();
        await generator.NextAsync();

        _clock.UtcNow = new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("ABR-1406-000001", await generator.NextAsync());
    }

    [Fact]
    public async Task Parallel_calls_produce_unique_numbers()
    {
        const int count = 200;
        var generator = CreateGenerator();

        var numbers = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(() => generator.NextAsync())));

        Assert.Equal(count, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.Matches(@"^ABR-1405-\d{6}$", n));
        Assert.Equal("ABR-1405-000200", numbers.Max());
    }

    [Fact]
    public async Task Racy_store_alone_duplicates_so_the_lock_is_what_guarantees_uniqueness()
    {
        // Guards the guard: proves RacySequenceStore really races, so the parallel test above is meaningful.
        var store = new RacySequenceStore();

        var values = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => store.IncrementAsync("k"))));

        Assert.True(values.Distinct().Count() < values.Length);
    }
}

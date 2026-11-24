using System.Collections.Concurrent;
using AbrIT.Panel.Domain.Orders;

namespace AbrIT.Panel.Domain.Tests.Orders;

internal sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class CountingIdSource : IIdSource
{
    private int _next;

    public string NewId() => $"id{Interlocked.Increment(ref _next):D4}";
}

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public int SaveCount { get; private set; }

    public Task<Order?> GetAsync(string orderId, CancellationToken ct = default) =>
        Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<IReadOnlyList<Order>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(
            [.. _orders.Values.Where(o => o.OwnerUserId == ownerUserId).OrderByDescending(o => o.CreatedUtc)]);

    public Task SaveAsync(Order order, CancellationToken ct = default)
    {
        _orders[order.OrderId] = order;
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePlanCatalog : IPlanCatalog
{
    public Dictionary<string, PlanInfo> Plans { get; } = new()
    {
        ["abrplan-drive-starter"] = new(new PlanSnapshot("drive-starter", "درایو – پایه", 99_000, 1024, 1), true),
        ["abrplan-drive-team"] = new(new PlanSnapshot("drive-team", "درایو – تیمی", 299_000, 500, 3), true),
        ["abrplan-retired"] = new(new PlanSnapshot("retired", "قدیمی", 10_000, 100, 1), false),
    };

    public Task<PlanInfo?> GetAsync(string planContentItemId, CancellationToken ct = default) =>
        Task.FromResult(Plans.GetValueOrDefault(planContentItemId));
}

/// <summary>Per-key async lock: the in-process equivalent of Orchard's IDistributedLock.</summary>
internal sealed class SemaphoreOrderLock : IOrderLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Deliberately non-atomic read / yield / write, so duplicates appear unless the caller holds the lock.
/// </summary>
internal sealed class RacySequenceStore : ISequenceStore
{
    private readonly Dictionary<string, long> _values = [];

    public async Task<long> IncrementAsync(string key, CancellationToken ct = default)
    {
        long current;
        lock (_values)
        {
            current = _values.GetValueOrDefault(key);
        }

        await Task.Delay(1, ct);

        lock (_values)
        {
            _values[key] = current + 1;
        }

        return current + 1;
    }
}

internal sealed class InMemoryProvisioningRequestRepository : AbrIT.Panel.Domain.Provisioning.IProvisioningRequestRepository
{
    private readonly ConcurrentDictionary<string, AbrIT.Panel.Domain.Provisioning.ProvisioningRequest> _requests = new();

    public int Count => _requests.Count;

    public Task<AbrIT.Panel.Domain.Provisioning.ProvisioningRequest?> GetByOrderIdAsync(string orderId, CancellationToken ct = default) =>
        Task.FromResult(_requests.GetValueOrDefault(orderId));

    public Task SaveAsync(AbrIT.Panel.Domain.Provisioning.ProvisioningRequest request, CancellationToken ct = default)
    {
        // A second document for the same order would be the idempotency bug these tests guard against.
        var existing = _requests.GetOrAdd(request.OrderId, request);
        if (existing.RequestId != request.RequestId)
        {
            throw new InvalidOperationException("Duplicate ProvisioningRequest for one order.");
        }

        _requests[request.OrderId] = request;

        return Task.CompletedTask;
    }
}

/// <summary>Only the listed user ids hold the operator permissions.</summary>
internal sealed class FakeOperatorAuthorizer(params string[] admins) : IOperatorAuthorizer
{
    public Task<bool> IsAuthorizedAsync(string actorUserId, OperatorAction action, CancellationToken ct = default) =>
        Task.FromResult(admins.Contains(actorUserId));
}

internal sealed class FakeCustomerDirectory : AbrIT.Panel.Domain.Provisioning.ICustomerDirectory
{
    public Task<string?> GetEmailAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<string?>($"{userId}@example.test");
}

/// <summary>Stores a serialized copy, so unsaved changes to a loaded instance never leak (like a database).</summary>
internal sealed class InMemoryTeamWorkspaceRepository : AbrIT.Panel.Domain.Workspaces.ITeamWorkspaceRepository
{
    private readonly ConcurrentDictionary<string, string> _json = new();
    private int _saveCount;

    public int SaveCount => Volatile.Read(ref _saveCount);

    public Task<AbrIT.Panel.Domain.Workspaces.TeamWorkspace?> GetByOrderIdAsync(string orderId, CancellationToken ct = default) =>
        Task.FromResult(_json.TryGetValue(orderId, out var json)
            ? System.Text.Json.JsonSerializer.Deserialize<AbrIT.Panel.Domain.Workspaces.TeamWorkspace>(json)
            : null);

    public Task SaveAsync(AbrIT.Panel.Domain.Workspaces.TeamWorkspace workspace, CancellationToken ct = default)
    {
        _json[workspace.OrderId] = System.Text.Json.JsonSerializer.Serialize(workspace);
        Interlocked.Increment(ref _saveCount);
        return Task.CompletedTask;
    }
}

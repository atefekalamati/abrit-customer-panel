using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace AbrIT.Panel.Module.Orders;

public sealed class OrderMigrations : DataMigration
{
    private const int IdLength = 64;

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OrderIndex>(table => table
            .Column<string>(nameof(OrderIndex.OrderId), c => c.WithLength(IdLength))
            .Column<string>(nameof(OrderIndex.OrderNumber), c => c.WithLength(32))
            .Column<string>(nameof(OrderIndex.OwnerUserId), c => c.WithLength(IdLength))
            .Column<string>(nameof(OrderIndex.Status), c => c.WithLength(32))
            .Column<DateTime>(nameof(OrderIndex.CreatedUtc)));

        await SchemaBuilder.AlterIndexTableAsync<OrderIndex>(table =>
        {
            table.CreateIndex("IDX_OrderIndex_OrderId", "DocumentId", nameof(OrderIndex.OrderId));
            table.CreateIndex("IDX_OrderIndex_Owner", "DocumentId", nameof(OrderIndex.OwnerUserId), nameof(OrderIndex.CreatedUtc));
            table.CreateIndex("IDX_OrderIndex_Number", "DocumentId", nameof(OrderIndex.OrderNumber));
            table.CreateIndex("IDX_OrderIndex_Status", "DocumentId", nameof(OrderIndex.Status), nameof(OrderIndex.CreatedUtc));
        });

        return 1;
    }
}

using System.Text.Json.Nodes;
using AbrIT.Panel.Domain.Orders;
using OrchardCore.ContentManagement;

namespace AbrIT.Panel.Module.Orders;

/// <summary>Reads published <c>AbrPlan</c> content items (seeded with fixed ids, DECISIONS D-09).</summary>
public sealed class ContentPlanCatalog(IContentManager contentManager) : IPlanCatalog
{
    public const string ContentType = "AbrPlan";

    public async Task<PlanInfo?> GetAsync(string planContentItemId, CancellationToken ct = default)
    {
        var item = await contentManager.GetAsync(planContentItemId, VersionOptions.Published);
        if (item is null || item.ContentType != ContentType)
        {
            return null;
        }

        var part = JObject.FromObject(item)?[ContentType] as JsonObject;
        var planKey = part?["PlanKey"]?["Text"]?.GetValue<string>();
        if (part is null || string.IsNullOrWhiteSpace(planKey))
        {
            return null;
        }

        var snapshot = new PlanSnapshot(
            planKey,
            item.DisplayText ?? planKey,
            (long)Number(part, "PriceToman"),
            (int)Number(part, "QuotaPerUserMB"),
            (int)Number(part, "MaxUsers"));

        var isOrderable = part["IsOrderable"]?["Value"]?.GetValue<bool>() == true
            && snapshot.PriceToman >= 0
            && snapshot.QuotaPerUserMB > 0
            && snapshot.MaxUsers > 0;

        return new PlanInfo(snapshot, isOrderable);
    }

    private static decimal Number(JsonObject part, string field) =>
        part[field]?["Value"] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : 0;
}

using System.Text.Json.Nodes;
using AbrIT.Panel.Module.Orders;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace AbrIT.Panel.Module.Catalog;

public sealed record ServiceCard(
    string ContentItemId, string Title, string ServiceKey, string Availability, string? MoreInfoUrl, string? MoreInfoText, string? Summary);

public sealed record PlanCard(
    string ContentItemId, string Title, string PlanKey, long PriceToman, string BillingPeriod, int QuotaPerUserMB, int MaxUsers,
    bool IsOrderable, string? Description, string ServiceContentItemId);

/// <summary>Read-only view of the published catalog content items (AbrService / AbrPlan, DECISIONS D-02).</summary>
public sealed class CatalogReader(ISession session)
{
    public const string ServiceType = "AbrService";
    public const string DriveServiceKey = "drive";

    public async Task<IReadOnlyList<ServiceCard>> ListServicesAsync()
    {
        var items = await Published(ServiceType);
        return [.. items
            .Select(item => (item, part: Part(item, ServiceType)))
            .OrderBy(x => Number(x.part, "SortOrder"))
            .Select(x => new ServiceCard(
                x.item.ContentItemId,
                x.item.DisplayText ?? string.Empty,
                Text(x.part, "ServiceKey") ?? string.Empty,
                Text(x.part, "Availability") ?? "InfoOnly",
                x.part?["MoreInfoUrl"]?["Url"]?.GetValue<string>(),
                x.part?["MoreInfoUrl"]?["Text"]?.GetValue<string>(),
                Part(x.item, "MarkdownBodyPart")?["Source"]?.GetValue<string>()))];
    }

    public async Task<ServiceCard?> GetServiceAsync(string serviceKey) =>
        (await ListServicesAsync()).FirstOrDefault(s => s.ServiceKey == serviceKey);

    public async Task<IReadOnlyList<PlanCard>> ListPlansAsync(string serviceContentItemId)
    {
        var items = await Published(ContentPlanCatalog.ContentType);
        return [.. items.Select(ToPlan)
            .Where(p => p is not null && p.ServiceContentItemId == serviceContentItemId)
            .Select(p => p!)
            .OrderBy(p => p.PriceToman)];
    }

    public async Task<PlanCard?> GetPlanAsync(string planContentItemId)
    {
        var items = await Published(ContentPlanCatalog.ContentType);
        return items.Where(i => i.ContentItemId == planContentItemId).Select(ToPlan).FirstOrDefault();
    }

    private async Task<IEnumerable<ContentItem>> Published(string contentType) =>
        await session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentType && x.Published).ListAsync();

    private static PlanCard? ToPlan(ContentItem item)
    {
        var part = Part(item, ContentPlanCatalog.ContentType);
        var planKey = Text(part, "PlanKey");
        if (part is null || string.IsNullOrWhiteSpace(planKey))
        {
            return null;
        }

        var serviceId = (part["Service"]?["ContentItemIds"] as JsonArray)?.FirstOrDefault()?.GetValue<string>() ?? string.Empty;
        return new PlanCard(
            item.ContentItemId,
            item.DisplayText ?? planKey,
            planKey,
            (long)Number(part, "PriceToman"),
            Text(part, "BillingPeriod") ?? "Monthly",
            (int)Number(part, "QuotaPerUserMB"),
            (int)Number(part, "MaxUsers"),
            part["IsOrderable"]?["Value"]?.GetValue<bool>() == true,
            Text(part, "Description"),
            serviceId);
    }

    private static JsonObject? Part(ContentItem item, string name) => JObject.FromObject(item)?[name] as JsonObject;

    private static string? Text(JsonObject? part, string field) =>
        part?[field]?["Text"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static decimal Number(JsonObject? part, string field) =>
        part?[field]?["Value"] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : 0;
}

using System.Text.Json;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Webhooks;

/// <summary>
/// The one payload shape, built here and nowhere else.
///
/// <para><strong>Id plus minimal fields.</strong> No description, no comments, no attachments, no checklist and
/// no custom fields. A subscriber that needs more calls <c>GET /api/v1/workitems/{id}</c> with an API key —
/// which is why the two features ship together — and a smaller payload is a smaller thing to have promised
/// forever under the compatibility policy.</para>
///
/// <para>There is deliberately no per-subscription payload option. One shape, documented.</para>
/// </summary>
public static class WebhookPayloads
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <paramref name="entityName"/> is passed in rather than read off the navigation: the mutators load a work
    /// item without its related rows, and a payload whose fields depend on how the caller happened to fetch the
    /// entity is a payload that is sometimes right.
    /// </summary>
    public static string ForWorkItem(
        WebhookEventType type,
        Guid eventId,
        WorkItem item,
        string? entityName,
        DateTimeOffset at,
        bool late)
        => JsonSerializer.Serialize(
            new
            {
                id = eventId,
                type = WebhookEvents.WireName(type),
                timestamp = at,
                data = new
                {
                    workItemId = item.Id,
                    responsibilityId = item.ResponsibilityId,
                    title = item.Title,
                    status = item.Status.ToString(),
                    dueDate = item.DueDate,
                    periodStart = item.PeriodStart,
                    periodEnd = item.PeriodEnd,
                    ownerUserId = item.OwnerUserId,
                    entityId = item.EntityId,
                    entityName,
                    departmentId = item.DepartmentId,
                    holdReason = item.HoldReason?.ToString(),
                    late,
                },
            },
            JsonOptions);

    public static string ForEntity(WebhookEventType type, Guid eventId, Entity entity, DateTimeOffset at)
        => JsonSerializer.Serialize(
            new
            {
                id = eventId,
                type = WebhookEvents.WireName(type),
                timestamp = at,
                data = new
                {
                    entityId = entity.Id,
                    name = entity.Name,
                    entityType = entity.Type.ToString(),
                    active = entity.Active,
                },
            },
            JsonOptions);

    /// <summary>What the admin test button sends, so a receiver can be proved before it is trusted.</summary>
    public static string Ping(Guid eventId, DateTimeOffset at)
        => JsonSerializer.Serialize(
            new
            {
                id = eventId,
                type = WebhookEvents.WireName(WebhookEventType.Ping),
                timestamp = at,
                data = new { message = "Everdue webhook test." },
            },
            JsonOptions);
}

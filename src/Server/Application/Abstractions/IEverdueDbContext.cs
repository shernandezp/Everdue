using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// What the Application layer is allowed to know about persistence: the aggregates and a save.
/// The tenant filter, the provider and the migrations are Infrastructure's business — a handler
/// physically cannot reach them, which is what keeps every query tenant-safe by construction.
/// </summary>
public interface IEverdueDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<Department> Departments { get; }

    DbSet<Entity> Entities { get; }

    DbSet<Responsibility> Responsibilities { get; }

    DbSet<ResponsibilityEvent> ResponsibilityEvents { get; }

    DbSet<WorkItem> WorkItems { get; }

    DbSet<WorkItemEvent> WorkItemEvents { get; }

    DbSet<Comment> Comments { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<NotificationDelivery> NotificationDeliveries { get; }

    DbSet<DigestSubscription> DigestSubscriptions { get; }

    DbSet<Attachment> Attachments { get; }

    DbSet<SavedView> SavedViews { get; }

    DbSet<ChecklistTemplateItem> ChecklistTemplateItems { get; }

    DbSet<ChecklistItem> ChecklistItems { get; }

    DbSet<EntityFieldDef> EntityFieldDefs { get; }

    DbSet<ApiKey> ApiKeys { get; }

    DbSet<WebhookSubscription> WebhookSubscriptions { get; }

    DbSet<WebhookDelivery> WebhookDeliveries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

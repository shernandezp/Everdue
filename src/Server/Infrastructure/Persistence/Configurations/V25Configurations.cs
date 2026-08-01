using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Everdue.Server.Infrastructure.Persistence.Configurations;

public sealed class ChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ChecklistTemplateItem>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateItem> builder)
    {
        builder.ToTable("ChecklistTemplateItems");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Text).HasMaxLength(300).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.ResponsibilityId, t.Position });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Responsibility).WithMany().HasForeignKey(t => t.ResponsibilityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("ChecklistItems");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Text).HasMaxLength(300).IsRequired();

        // The progress reader groups by WorkItemId over a set of ids, so the work item leads the index.
        builder.HasIndex(c => new { c.TenantId, c.WorkItemId, c.Position });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.WorkItem).WithMany().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EntityFieldDefConfiguration : IEntityTypeConfiguration<EntityFieldDef>
{
    public void Configure(EntityTypeBuilder<EntityFieldDef> builder)
    {
        builder.ToTable("EntityFieldDefs");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(50).IsRequired();
        builder.Property(d => d.EntityType).HasConversion<int>();
        builder.Property(d => d.FieldType).HasConversion<int>();
        builder.Property(d => d.OptionsJson).HasMaxLength(1000);

        builder.HasIndex(d => new { d.TenantId, d.EntityType, d.Name }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Name).HasMaxLength(100).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(32).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.Scope).HasConversion<int>();

        // Not unique on purpose: with a 256-bit secret a prefix collision is a curiosity, and a unique index
        // would turn it into a failed key creation. The store verifies the hash against every match.
        builder.HasIndex(k => k.KeyPrefix);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(k => k.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(k => k.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Url).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SecretProtected).IsRequired();
        builder.Property(s => s.EventTypes).HasMaxLength(200).IsRequired();
        builder.Property(s => s.LastError).HasMaxLength(500);

        builder.HasIndex(s => new { s.TenantId, s.Active });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.EventType).HasConversion<int>();
        builder.Property(d => d.Status).HasConversion<int>();
        builder.Property(d => d.PayloadJson).HasMaxLength(4000).IsRequired();
        builder.Property(d => d.LastError).HasMaxLength(500);

        // The dispatcher's only query. Deliberately not tenant-led: the dispatcher drains the outbox across the
        // instance, exactly as the notification one does.
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Subscription).WithMany().HasForeignKey(d => d.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

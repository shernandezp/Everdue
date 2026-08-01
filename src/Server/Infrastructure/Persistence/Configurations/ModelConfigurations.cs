using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Everdue.Server.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(t => t.DefaultLanguage).HasMaxLength(8).IsRequired();
    }
}

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PreferredLanguage).HasMaxLength(8);
        builder.Property(u => u.NotificationPreferencesJson).HasMaxLength(2000);
        builder.Property(u => u.TelegramLinkCode).HasMaxLength(16);
        builder.Property(u => u.WhatsAppPhoneE164).HasMaxLength(20);
        builder.HasIndex(u => new { u.TenantId, u.Active });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => new { d.TenantId, d.Name }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    public void Configure(EntityTypeBuilder<Entity> builder)
    {
        builder.ToTable("Entities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Type).HasConversion<int>();

        // Display-only reference values. Bounded by the ten-fields-per-type cap and never queried, so the
        // column is a string and there is no EAV table (guardrails §2).
        builder.Property(e => e.CustomFieldsJson).HasMaxLength(4000);

        builder.HasIndex(e => new { e.TenantId, e.Type, e.Name }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ResponsibilityConfiguration : IEntityTypeConfiguration<Responsibility>
{
    public void Configure(EntityTypeBuilder<Responsibility> builder)
    {
        builder.ToTable("Responsibilities");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(300).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(4000);
        builder.Property(r => r.RecurrenceKind).HasConversion<int>();

        builder.HasIndex(r => new { r.TenantId, r.Active });
        builder.HasIndex(r => new { r.TenantId, r.OwnerUserId });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(r => r.Entity).WithMany().HasForeignKey(r => r.EntityId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Title).HasMaxLength(300).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(4000);
        builder.Property(w => w.Status).HasConversion<int>();
        builder.Property(w => w.HoldReason).HasConversion<int>();
        builder.Property(w => w.HoldReasonText).HasMaxLength(1000);

        // The engine's idempotency guarantee: a double tick or a racing instance loses harmlessly.
        // One-off rows carry NULLs in both columns and NULLs are distinct in a unique index on
        // both providers, so they never collide.
        builder.HasIndex(w => new { w.ResponsibilityId, w.PeriodStart })
            .IsUnique()
            .HasDatabaseName("IX_WorkItems_Responsibility_PeriodStart");

        builder.HasIndex(w => new { w.TenantId, w.Status, w.DueDate });
        builder.HasIndex(w => new { w.TenantId, w.EntityId, w.CompletedAt });
        builder.HasIndex(w => new { w.TenantId, w.OwnerUserId, w.Status });

        // Every insight metric is "occurrences whose period starts inside a window, grouped by
        // something". The unique index above leads with the responsibility, so it cannot range-scan a
        // window across all of them, and the status index leads with status. Plain composite rather
        // than included columns: IncludeProperties is Postgres-only and would break the SQLite leg.
        builder.HasIndex(w => new { w.TenantId, w.PeriodStart, w.Status });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Responsibility).WithMany().HasForeignKey(w => w.ResponsibilityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(w => w.Entity).WithMany().HasForeignKey(w => w.EntityId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(w => w.Department).WithMany().HasForeignKey(w => w.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(w => w.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkItemEventConfiguration : IEntityTypeConfiguration<WorkItemEvent>
{
    public void Configure(EntityTypeBuilder<WorkItemEvent> builder)
    {
        builder.ToTable("WorkItemEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventType).HasConversion<int>();
        builder.Property(e => e.FromStatus).HasConversion<int>();
        builder.Property(e => e.ToStatus).HasConversion<int>();
        builder.Property(e => e.DataJson).HasMaxLength(4000);

        builder.HasIndex(e => new { e.WorkItemId, e.Timestamp });
        builder.HasIndex(e => new { e.TenantId, e.EventType, e.Timestamp });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WorkItem).WithMany().HasForeignKey(e => e.WorkItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Body).HasMaxLength(4000).IsRequired();

        builder.HasIndex(c => new { c.WorkItemId, c.CreatedAt });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.WorkItem).WithMany().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

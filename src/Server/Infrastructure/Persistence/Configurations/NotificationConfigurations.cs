using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Everdue.Server.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<int>();
        builder.Property(n => n.DataJson).HasMaxLength(2000);
        builder.Property(n => n.DedupeKey).HasMaxLength(200);

        // The bell's only query: this user's rows, unread first, newest first.
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.ReadAt, n.CreatedAt });

        // The idempotency guarantee for anything that must happen once per thing per day. NULLs are
        // distinct on both providers, so repeatable notifications never collide with each other.
        builder.HasIndex(n => new { n.TenantId, n.DedupeKey })
            .IsUnique()
            .HasDatabaseName("IX_Notifications_Tenant_DedupeKey");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.WorkItem).WithMany().HasForeignKey(n => n.WorkItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Channel).HasConversion<int>();
        builder.Property(d => d.Status).HasConversion<int>();
        builder.Property(d => d.LastError).HasMaxLength(500);

        // The dispatcher's only query.
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });

        // The health screen's only query.
        builder.HasIndex(d => new { d.TenantId, d.Channel, d.Status });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Notification).WithMany().HasForeignKey(d => d.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DigestSubscriptionConfiguration : IEntityTypeConfiguration<DigestSubscription>
{
    public void Configure(EntityTypeBuilder<DigestSubscription> builder)
    {
        builder.ToTable("DigestSubscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Frequency).HasConversion<int>();
        builder.Property(s => s.WeeklyDayOfWeek).HasConversion<int>();

        // One subscription per person: edited, never accumulated.
        builder.HasIndex(s => new { s.TenantId, s.UserId }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Department).WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ChannelSettingsConfiguration : IEntityTypeConfiguration<ChannelSettings>
{
    public void Configure(EntityTypeBuilder<ChannelSettings> builder)
    {
        builder.ToTable("ChannelSettings");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Channel).HasConversion<int>();
        builder.Property(c => c.ConfigProtected).HasMaxLength(8000).IsRequired();

        // Exactly one row per scope per channel. Works precisely because system scope is Guid.Empty
        // and not NULL — see ChannelSettings for why that matters.
        builder.HasIndex(c => new { c.TenantId, c.Channel }).IsUnique();

        // No tenant FK: the system-scope row's TenantId (Guid.Empty) points at no tenant by design.
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(200).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.WorkItemId, a.CreatedAt });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.WorkItem).WithMany().HasForeignKey(a => a.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("SavedViews");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Route).HasMaxLength(20).IsRequired();
        builder.Property(v => v.QueryString).HasMaxLength(1000).IsRequired();

        builder.HasIndex(v => new { v.TenantId, v.UserId, v.Name }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(v => v.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

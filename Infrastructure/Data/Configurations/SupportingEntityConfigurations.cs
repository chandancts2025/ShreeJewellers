using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShreeJewellers.Domain.Entities;

namespace ShreeJewellers.Infrastructure.Data.Configurations;

public class GoldPriceHistoryConfiguration : IEntityTypeConfiguration<GoldPriceHistory>
{
    public void Configure(EntityTypeBuilder<GoldPriceHistory> builder)
    {
        builder.ToTable("GoldPriceHistory");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CreatedByUserId).HasMaxLength(450);
        builder.Property(p => p.Source).HasConversion<int>();
        builder.Property(p => p.GoldRatePer10Gram_22K).HasColumnType("decimal(10,2)");
        builder.Property(p => p.GoldRatePer10Gram_24K).HasColumnType("decimal(10,2)");
        builder.Property(p => p.SilverRatePerKg).HasColumnType("decimal(10,2)");
        builder.Property(p => p.RecordedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(p => p.RecordedAt).HasDatabaseName("IX_GoldPriceHistory_RecordedAt");
        builder.HasIndex(p => new { p.RecordedAt, p.Source })
            .HasDatabaseName("IX_GoldPriceHistory_Date_Source");

        builder.HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired().HasMaxLength(450);
        builder.Property(n => n.Subject).IsRequired().HasMaxLength(300);
        builder.Property(n => n.Body).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(n => n.RelatedEntityType).HasMaxLength(100);
        builder.Property(n => n.Type).HasConversion<int>();
        builder.Property(n => n.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(n => n.UserId).HasDatabaseName("IX_Notifications_UserId");
        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_User_Read");  // Unread count queries

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).HasMaxLength(450);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IPAddress).HasMaxLength(50);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.Timestamp).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(a => a.Timestamp).HasDatabaseName("IX_AuditLogs_Timestamp");
        builder.HasIndex(a => a.UserId)
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("IX_AuditLogs_UserId");
        builder.HasIndex(a => new { a.EntityName, a.EntityId })
            .HasDatabaseName("IX_AuditLogs_Entity");
        builder.HasIndex(a => a.Action).HasDatabaseName("IX_AuditLogs_Action");

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SettingKey).IsRequired().HasMaxLength(100);
        builder.Property(s => s.SettingValue).IsRequired().HasMaxLength(2000);
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.UpdatedByUserId).HasMaxLength(450);

        builder.HasIndex(s => s.SettingKey).IsUnique().HasDatabaseName("IX_AppSettings_Key");

        builder.HasOne(s => s.UpdatedBy)
            .WithMany()
            .HasForeignKey(s => s.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

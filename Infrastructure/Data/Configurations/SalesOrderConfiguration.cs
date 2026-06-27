using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShreeJewellers.Domain.Entities;

namespace ShreeJewellers.Infrastructure.Data.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(30);
        builder.Property(o => o.CustomerUserId).HasMaxLength(450);
        builder.Property(o => o.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.InvoiceUrl).HasMaxLength(500);
        builder.Property(o => o.PaymentMode).HasConversion<int>();
        builder.Property(o => o.PaymentStatus).HasConversion<int>();
        builder.Property(o => o.Status).HasConversion<int>();

        builder.Property(o => o.GrossAmount).HasColumnType("decimal(14,2)");
        builder.Property(o => o.DiscountAmount).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.TaxAmount).HasColumnType("decimal(14,2)");
        builder.Property(o => o.OldGoldExchangeValue).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.NetAmount).HasColumnType("decimal(14,2)");
        builder.Property(o => o.AmountPaid).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.AdvanceAmount).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.CGSTAmount).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.SGSTAmount).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.IGSTAmount).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(o => o.OrderNumber).IsUnique().HasDatabaseName("IX_SalesOrders_OrderNumber");
        builder.HasIndex(o => o.CustomerUserId)
            .HasFilter("[CustomerUserId] IS NOT NULL")
            .HasDatabaseName("IX_SalesOrders_CustomerUserId");
        builder.HasIndex(o => o.OrderDate).HasDatabaseName("IX_SalesOrders_OrderDate");
        builder.HasIndex(o => o.Status).HasDatabaseName("IX_SalesOrders_Status");
        builder.HasIndex(o => new { o.OrderDate, o.Status })
            .HasDatabaseName("IX_SalesOrders_Date_Status");  // For daily sales reports

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(o => o.Customer)
            .WithMany(u => u.SalesOrders)
            .HasForeignKey(o => o.CustomerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.SalesOrder)
            .HasForeignKey(i => i.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("SalesOrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.WeightGrams).HasColumnType("decimal(10,3)");
        builder.Property(i => i.RatePerGram).HasColumnType("decimal(10,2)");
        builder.Property(i => i.MakingCharges).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(i => i.HallmarkCharges).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(i => i.StoneValue).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(i => i.TaxPercent).HasColumnType("decimal(5,2)").HasDefaultValue(3m);
        builder.Property(i => i.TaxAmount).HasColumnType("decimal(10,2)");
        builder.Property(i => i.DiscountAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(i => i.LineTotal).HasColumnType("decimal(14,2)");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(i => i.SalesOrderId).HasDatabaseName("IX_SalesOrderItems_OrderId");
        builder.HasIndex(i => i.ProductId).HasDatabaseName("IX_SalesOrderItems_ProductId");

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(i => i.Product)
            .WithMany(p => p.SalesOrderItems)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

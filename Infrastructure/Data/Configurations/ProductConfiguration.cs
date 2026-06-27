using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;

namespace ShreeJewellers.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.MetalType).HasConversion<int>();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("IX_Categories_Name");
        builder.HasIndex(c => c.IsActive).HasDatabaseName("IX_Categories_IsActive");
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.SKUCode).IsRequired().HasMaxLength(50);
        builder.Property(p => p.HSNCode).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Purity).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.HallmarkNumber).HasMaxLength(50);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.BarcodeData).HasMaxLength(200);
        builder.Property(p => p.MakingChargesType).HasConversion<int>();

        builder.Property(p => p.NetWeightGrams).HasColumnType("decimal(10,3)");
        builder.Property(p => p.GrossWeightGrams).HasColumnType("decimal(10,3)");
        builder.Property(p => p.StoneWeightGrams).HasColumnType("decimal(10,3)").HasDefaultValue(0m);
        builder.Property(p => p.MakingChargesValue).HasColumnType("decimal(10,2)");
        builder.Property(p => p.WastagePercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(p => p.HallmarkCharges).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(p => p.GSTRatePercent).HasColumnType("decimal(5,2)").HasDefaultValue(3m);
        builder.Property(p => p.StoneValue).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(p => p.StockQuantity).HasDefaultValue(0);
        builder.Property(p => p.ReorderLevel).HasDefaultValue(2);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(p => p.SKUCode).IsUnique().HasDatabaseName("IX_Products_SKUCode");
        builder.HasIndex(p => p.CategoryId).HasDatabaseName("IX_Products_CategoryId");
        builder.HasIndex(p => p.IsActive).HasDatabaseName("IX_Products_IsActive");
        builder.HasIndex(p => new { p.IsActive, p.StockQuantity })
            .HasDatabaseName("IX_Products_Active_Stock");  // For low-stock queries

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TransactionType).HasConversion<int>();
        builder.Property(t => t.WeightGrams).HasColumnType("decimal(10,3)");
        builder.Property(t => t.RatePerGram).HasColumnType("decimal(10,2)");
        builder.Property(t => t.TotalValue).HasColumnType("decimal(14,2)");
        builder.Property(t => t.ReferenceNo).HasMaxLength(50);
        builder.Property(t => t.Notes).HasMaxLength(500);
        builder.Property(t => t.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(t => t.ProductId).HasDatabaseName("IX_InvTrans_ProductId");
        builder.HasIndex(t => t.CreatedAt).HasDatabaseName("IX_InvTrans_CreatedAt");
        builder.HasIndex(t => t.TransactionType).HasDatabaseName("IX_InvTrans_Type");
        builder.HasIndex(t => t.ReferenceNo)
            .HasFilter("[ReferenceNo] IS NOT NULL")
            .HasDatabaseName("IX_InvTrans_ReferenceNo");

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(t => t.Product)
            .WithMany(p => p.InventoryTransactions)
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

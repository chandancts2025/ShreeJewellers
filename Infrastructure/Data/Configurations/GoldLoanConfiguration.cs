using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShreeJewellers.Domain.Entities;

namespace ShreeJewellers.Infrastructure.Data.Configurations;

public class GoldLoanConfiguration : IEntityTypeConfiguration<GoldLoan>
{
    public void Configure(EntityTypeBuilder<GoldLoan> builder)
    {
        builder.ToTable("GoldLoans");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LoanNumber).IsRequired().HasMaxLength(30);
        builder.Property(l => l.CustomerUserId).IsRequired().HasMaxLength(450);
        builder.Property(l => l.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(l => l.GoldPurity).IsRequired().HasMaxLength(20);
        builder.Property(l => l.Notes).HasMaxLength(1000);
        builder.Property(l => l.LoanStatus).HasConversion<int>();

        builder.Property(l => l.GoldDepositedWeightGrams).HasColumnType("decimal(10,3)");
        builder.Property(l => l.GoldCurrentValueAtDeposit).HasColumnType("decimal(14,2)");
        builder.Property(l => l.PrincipalAmount).HasColumnType("decimal(14,2)");
        builder.Property(l => l.LoanToValuePercent).HasColumnType("decimal(5,2)");
        builder.Property(l => l.TotalRepaid).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(l => l.ExtensionCount).HasDefaultValue(0);
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(l => l.LoanNumber).IsUnique().HasDatabaseName("IX_GoldLoans_LoanNumber");
        builder.HasIndex(l => l.CustomerUserId).HasDatabaseName("IX_GoldLoans_CustomerId");
        builder.HasIndex(l => l.LoanStatus).HasDatabaseName("IX_GoldLoans_Status");
        builder.HasIndex(l => l.MaturityDate).HasDatabaseName("IX_GoldLoans_MaturityDate");
        builder.HasIndex(l => new { l.LoanStatus, l.MaturityDate })
            .HasDatabaseName("IX_GoldLoans_Status_MaturityDate");  // For due/overdue queries
        builder.HasIndex(l => new { l.CustomerUserId, l.LoanStatus })
            .HasDatabaseName("IX_GoldLoans_Customer_Status");

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(l => l.Customer)
            .WithMany(u => u.GoldLoans)
            .HasForeignKey(l => l.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.CreatedBy)
            .WithMany()
            .HasForeignKey(l => l.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.InterestSetting)
            .WithMany(s => s.GoldLoans)
            .HasForeignKey(l => l.InterestSettingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.GoldItems)
            .WithOne(i => i.GoldLoan)
            .HasForeignKey(i => i.GoldLoanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(l => l.Repayments)
            .WithOne(r => r.GoldLoan)
            .HasForeignKey(r => r.GoldLoanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoldLoanItemConfiguration : IEntityTypeConfiguration<GoldLoanItem>
{
    public void Configure(EntityTypeBuilder<GoldLoanItem> builder)
    {
        builder.ToTable("GoldLoanItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemDescription).IsRequired().HasMaxLength(500);
        builder.Property(i => i.Purity).IsRequired().HasMaxLength(20);
        builder.Property(i => i.ImageUrl).HasMaxLength(500);
        builder.Property(i => i.HallmarkNumber).HasMaxLength(200);
        builder.Property(i => i.WeightGrams).HasColumnType("decimal(10,3)");
        builder.Property(i => i.EstimatedValue).HasColumnType("decimal(14,2)");

        builder.HasIndex(i => i.GoldLoanId).HasDatabaseName("IX_GoldLoanItems_LoanId");
    }
}

public class LoanRepaymentConfiguration : IEntityTypeConfiguration<LoanRepayment>
{
    public void Configure(EntityTypeBuilder<LoanRepayment> builder)
    {
        builder.ToTable("LoanRepayments");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.GoldLoanId).IsRequired();
        builder.Property(r => r.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.ReceiptNumber).IsRequired().HasMaxLength(30);
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.Property(r => r.PaymentMode).HasConversion<int>();

        builder.Property(r => r.AmountPaid).HasColumnType("decimal(14,2)");
        builder.Property(r => r.PrincipalComponent).HasColumnType("decimal(14,2)");
        builder.Property(r => r.InterestComponent).HasColumnType("decimal(14,2)");
        builder.Property(r => r.PenaltyAmount).HasColumnType("decimal(14,2)").HasDefaultValue(0m);
        builder.Property(r => r.PrincipalBalanceAfter).HasColumnType("decimal(14,2)");
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(r => r.ReceiptNumber).IsUnique().HasDatabaseName("IX_LoanRepayments_Receipt");
        builder.HasIndex(r => r.GoldLoanId).HasDatabaseName("IX_LoanRepayments_LoanId");
        builder.HasIndex(r => r.RepaymentDate).HasDatabaseName("IX_LoanRepayments_Date");

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasOne(r => r.GoldLoan)
            .WithMany(l => l.Repayments)
            .HasForeignKey(r => r.GoldLoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CreatedBy)
            .WithMany(u => u.LoanRepayments)
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InterestSettingConfiguration : IEntityTypeConfiguration<InterestSetting>
{
    public void Configure(EntityTypeBuilder<InterestSetting> builder)
    {
        builder.ToTable("InterestSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(s => s.Notes).HasMaxLength(500);
        builder.Property(s => s.CalculationType).HasConversion<int>();
        builder.Property(s => s.InterestRatePercent).HasColumnType("decimal(5,2)");
        builder.Property(s => s.PenaltyRatePercent).HasColumnType("decimal(5,2)").HasDefaultValue(1m);
        builder.Property(s => s.LoanToValuePercent).HasColumnType("decimal(5,2)").HasDefaultValue(75m);
        builder.Property(s => s.DefaultTenureMonths).HasDefaultValue(12);
        builder.Property(s => s.CompoundingEnabled).HasDefaultValue(false);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // Partial unique index — only one active setting allowed (EffectiveTo IS NULL)
        builder.HasIndex(s => s.EffectiveTo)
            .HasFilter("[EffectiveTo] IS NULL")
            .HasDatabaseName("IX_InterestSettings_ActiveSetting");

        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

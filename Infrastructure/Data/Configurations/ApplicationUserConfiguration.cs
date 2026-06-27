using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShreeJewellers.Domain.Entities;

namespace ShreeJewellers.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Gender).IsRequired().HasMaxLength(20);
        builder.Property(u => u.CustomerCode).HasMaxLength(20);
        builder.Property(u => u.ProfilePhotoUrl).HasMaxLength(500);
        builder.Property(u => u.IDProofUrl).HasMaxLength(500);
        builder.Property(u => u.AadhaarNumberEncrypted).HasMaxLength(500);
        builder.Property(u => u.PANNumberEncrypted).HasMaxLength(500);
        builder.Property(u => u.AddressLine1).IsRequired().HasMaxLength(200);
        builder.Property(u => u.AddressLine2).HasMaxLength(200);
        builder.Property(u => u.City).IsRequired().HasMaxLength(100);
        builder.Property(u => u.State).IsRequired().HasMaxLength(100);
        builder.Property(u => u.PinCode).IsRequired().HasMaxLength(10);
        builder.Property(u => u.AlternatePhone).HasMaxLength(15);
        builder.Property(u => u.LastLoginIP).HasMaxLength(50);
        builder.Property(u => u.KYCRejectionReason).HasMaxLength(500);
        builder.Property(u => u.KYCStatus).HasConversion<int>();
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // ── Unique Indexes ────────────────────────────────────────────────
        builder.HasIndex(u => u.CustomerCode)
            .IsUnique()
            .HasFilter("[CustomerCode] IS NOT NULL")
            .HasDatabaseName("IX_Users_CustomerCode");

        builder.HasIndex(u => u.KYCStatus)
            .HasDatabaseName("IX_Users_KYCStatus");

        builder.HasIndex(u => u.IsActive)
            .HasDatabaseName("IX_Users_IsActive");

        // ── Self-referencing relationships ────────────────────────────────
        builder.HasOne(u => u.ReferredBy)
            .WithMany()
            .HasForeignKey(u => u.ReferredByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.KYCVerifiedBy)
            .WithMany()
            .HasForeignKey(u => u.KYCVerifiedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

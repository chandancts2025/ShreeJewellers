using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data.Configurations;

namespace ShreeJewellers.Infrastructure.Data;

/// <summary>
/// Main EF Core DbContext for Shree Jewellers.
/// Extends IdentityDbContext so ASP.NET Core Identity tables are managed here.
/// All entity configurations are in separate IEntityTypeConfiguration classes.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<GoldLoan> GoldLoans => Set<GoldLoan>();
    public DbSet<GoldLoanItem> GoldLoanItems => Set<GoldLoanItem>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();
    public DbSet<InterestSetting> InterestSettings => Set<InterestSetting>();
    public DbSet<GoldPriceHistory> GoldPriceHistory => Set<GoldPriceHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // ── OnModelCreating ───────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);  // MUST call base — applies Identity table configs

        // Rename default Identity tables to cleaner names
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");

        // Apply all IEntityTypeConfiguration classes from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    // ── SaveChanges — Auto-set audit timestamps ───────────────────────────

    public override int SaveChanges()
    {
        SetAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditTimestamps()
    {
        var entries = ChangeTracker.Entries<IAuditableEntity>();
        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
    }
}

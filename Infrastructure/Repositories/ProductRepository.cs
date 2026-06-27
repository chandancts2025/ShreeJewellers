using Microsoft.EntityFrameworkCore;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Application.DTOs;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;

namespace ShreeJewellers.Infrastructure.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCT REPOSITORY
// ─────────────────────────────────────────────────────────────────────────────

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<Product?> GetBySkuAsync(string skuCode);
    Task<(List<Product> items, int total)> SearchAsync(ProductSearchDto filter);
    Task<List<Product>> GetLowStockAsync();
    Task<List<Product>> GetByCategoryAsync(int categoryId);
    Task<List<Category>> GetCategoriesAsync();
    Task<bool> SkuExistsAsync(string sku, int? excludeId = null);
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task SaveAsync();
}

public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _db;
    public ProductRepository(ApplicationDbContext db) => _db = db;

    public async Task<Product?> GetByIdAsync(int id)
        => await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product?> GetBySkuAsync(string skuCode)
        => await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.SKUCode == skuCode && p.IsActive);

    public async Task<(List<Product> items, int total)> SearchAsync(ProductSearchDto filter)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(q) ||
                p.SKUCode.ToLower().Contains(q) ||
                (p.HallmarkNumber != null && p.HallmarkNumber.ToLower().Contains(q)) ||
                p.Purity.ToLower().Contains(q));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrEmpty(filter.Purity))
            query = query.Where(p => p.Purity == filter.Purity);

        if (filter.InStockOnly == true)
            query = query.Where(p => p.StockQuantity > 0);

        if (filter.LowStockOnly == true)
            query = query.Where(p => p.StockQuantity <= p.ReorderLevel && p.IsActive);

        // Always show active products for non-admin queries (handled by controller role check)
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<Product>> GetLowStockAsync()
        => await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

    public async Task<List<Product>> GetByCategoryAsync(int categoryId)
        => await _db.Products
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<List<Category>> GetCategoriesAsync()
        => await _db.Categories
            .Include(c => c.Products.Where(p => p.IsActive))
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<bool> SkuExistsAsync(string sku, int? excludeId = null)
        => await _db.Products.AnyAsync(p =>
            p.SKUCode == sku && (excludeId == null || p.Id != excludeId));

    public async Task AddAsync(Product product) => await _db.Products.AddAsync(product);
    public Task UpdateAsync(Product p) { _db.Products.Update(p); return Task.CompletedTask; }
    public async Task SaveAsync() => await _db.SaveChangesAsync();
}

// ─────────────────────────────────────────────────────────────────────────────
// INVENTORY REPOSITORY
// ─────────────────────────────────────────────────────────────────────────────

public interface IInventoryRepository
{
    Task<List<InventoryTransaction>> GetByProductAsync(int productId, int page, int pageSize);
    Task<List<InventoryTransaction>> GetAllAsync(int page, int pageSize,
        DateTime? from = null, DateTime? to = null, string? type = null);
    Task AddAsync(InventoryTransaction transaction);
    Task SaveAsync();
}

public class InventoryRepository : IInventoryRepository
{
    private readonly ApplicationDbContext _db;
    public InventoryRepository(ApplicationDbContext db) => _db = db;

    public async Task<List<InventoryTransaction>> GetByProductAsync(
        int productId, int page, int pageSize)
        => await _db.InventoryTransactions
            .Include(t => t.Product)
            .Include(t => t.CreatedBy)
            .Where(t => t.ProductId == productId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<List<InventoryTransaction>> GetAllAsync(
        int page, int pageSize,
        DateTime? from = null, DateTime? to = null, string? type = null)
    {
        var query = _db.InventoryTransactions
            .Include(t => t.Product).ThenInclude(p => p.Category)
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (from.HasValue)  query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue)    query = query.Where(t => t.CreatedAt <= to.Value);
        if (!string.IsNullOrEmpty(type) &&
            Enum.TryParse<Domain.Enums.TransactionType>(type, out var tt))
            query = query.Where(t => t.TransactionType == tt);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task AddAsync(InventoryTransaction t) => await _db.InventoryTransactions.AddAsync(t);
    public async Task SaveAsync() => await _db.SaveChangesAsync();
}

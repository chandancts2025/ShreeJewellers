using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Repositories;

namespace ShreeJewellers.Infrastructure.Services;

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCT SERVICE
// ─────────────────────────────────────────────────────────────────────────────

public interface IProductService
{
    Task<ProductResponseDto> CreateAsync(CreateProductDto dto, string userId);
    Task<ProductResponseDto> UpdateAsync(UpdateProductDto dto, string userId);
    Task<ProductResponseDto> GetByIdAsync(int id);
    Task<ProductResponseDto?> GetBySkuAsync(string sku);
    Task<(List<ProductSummaryDto> items, int total)> SearchAsync(ProductSearchDto filter);
    Task<List<ProductSummaryDto>> GetLowStockAsync();
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task SoftDeleteAsync(int id, string userId);
    Task<string> GenerateQrCodeAsync(int productId);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly IInventoryRepository _invRepo;
    private readonly IPriceService _priceService;
    private readonly IBarcodeService _barcodeService;
    private readonly IFileUploadService _fileService;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository repo, IInventoryRepository invRepo,
        IPriceService priceService, IBarcodeService barcodeService,
        IFileUploadService fileService, IMapper mapper,
        ApplicationDbContext db, ILogger<ProductService> logger)
    {
        _repo = repo; _invRepo = invRepo; _priceService = priceService;
        _barcodeService = barcodeService; _fileService = fileService;
        _mapper = mapper; _db = db; _logger = logger;
    }

    public async Task<ProductResponseDto> CreateAsync(CreateProductDto dto, string userId)
    {
        if (await _repo.SkuExistsAsync(dto.SKUCode))
            throw new InvalidOperationException($"SKU '{dto.SKUCode}' already exists.");

        // Validate category exists
        var category = await _db.Categories.FindAsync(dto.CategoryId);
        if (category == null || !category.IsActive)
            throw new KeyNotFoundException($"Category {dto.CategoryId} not found or inactive.");

        var product = new Product
        {
            CategoryId          = dto.CategoryId,
            Name                = dto.Name.Trim(),
            Description         = dto.Description?.Trim(),
            SKUCode             = dto.SKUCode.Trim().ToUpper(),
            HSNCode             = dto.HSNCode.Trim(),
            Purity              = dto.Purity.Trim(),
            NetWeightGrams      = dto.NetWeightGrams,
            GrossWeightGrams    = dto.GrossWeightGrams,
            StoneWeightGrams    = dto.StoneWeightGrams,
            HallmarkNumber      = dto.HallmarkNumber?.Trim(),
            MakingChargesType   = dto.MakingChargesType,
            MakingChargesValue  = dto.MakingChargesValue,
            WastagePercent      = dto.WastagePercent,
            HallmarkCharges     = dto.HallmarkCharges,
            GSTRatePercent      = dto.GSTRatePercent,
            StoneValue          = dto.StoneValue,
            StockQuantity       = dto.StockQuantity,
            ReorderLevel        = dto.ReorderLevel,
            IsActive            = true,
            CreatedAt           = DateTime.UtcNow
        };

        // Calculate gross weight if not provided or invalid
        if (product.GrossWeightGrams < product.NetWeightGrams + product.StoneWeightGrams)
        {
            product.GrossWeightGrams = product.NetWeightGrams + product.StoneWeightGrams;
        }

        // Handle image upload with error handling
        if (!string.IsNullOrEmpty(dto.ImageBase64) && !string.IsNullOrEmpty(dto.ImageExtension))
        {
            try
            {
                product.ImageUrl = await _fileService.UploadBase64Async(
                    dto.ImageBase64, dto.ImageExtension, "products", dto.SKUCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image upload failed for SKU {SKU}", dto.SKUCode);
                throw new InvalidOperationException("Image upload failed. Please try again.", ex);
            }
        }

        await _repo.AddAsync(product);
        await _repo.SaveAsync();

        // Generate barcode data and QR code
        try
        {
            product.BarcodeData = _barcodeService.BuildBarcodeData(product.SKUCode, product.Id, product.Purity);
            await _repo.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Barcode generation failed for product {ProductId}", product.Id);
            // Don't fail the entire request; barcode is non-critical
        }

        // Create opening stock inventory entry
        if (dto.StockQuantity > 0)
        {
            try
            {
                var price = await _priceService.GetCurrentRateAsync();
                if (price == null)
                    throw new InvalidOperationException("Unable to retrieve current gold prices. Please try again.");

                var rate = product.Purity.StartsWith("24") ? price.Rate24KPer10g / 10 : price.Rate22KPer10g / 10;

                await _invRepo.AddAsync(new InventoryTransaction
                {
                    ProductId       = product.Id,
                    TransactionType = TransactionType.Purchase,
                    Quantity        = dto.StockQuantity,
                    WeightGrams     = dto.NetWeightGrams * dto.StockQuantity,
                    RatePerGram     = rate,
                    TotalValue      = dto.NetWeightGrams * dto.StockQuantity * rate,
                    ReferenceNo     = "OPENING-STOCK",
                    Notes           = "Opening stock on product creation",
                    CreatedByUserId = userId,
                    CreatedAt       = DateTime.UtcNow
                });
                await _invRepo.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Opening stock creation failed for product {ProductId}", product.Id);
                throw new InvalidOperationException("Failed to create opening stock entry.", ex);
            }
        }

        _logger.LogInformation("Product {SKU} created by {UserId}", product.SKUCode, userId);
        var createdProduct = await _repo.GetByIdAsync(product.Id) ?? product;
        return await EnrichWithPriceAsync(createdProduct);
    }

    public async Task<ProductResponseDto> UpdateAsync(UpdateProductDto dto, string userId)
    {
        var product = await _repo.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Product {dto.Id} not found.");

        if (await _repo.SkuExistsAsync(dto.SKUCode, dto.Id))
            throw new InvalidOperationException($"SKU '{dto.SKUCode}' is already used by another product.");

        product.CategoryId         = dto.CategoryId;
        product.Name               = dto.Name.Trim();
        product.Description        = dto.Description?.Trim();
        product.SKUCode            = dto.SKUCode.Trim().ToUpper();
        product.HSNCode            = dto.HSNCode.Trim();
        product.Purity             = dto.Purity.Trim();
        product.NetWeightGrams     = dto.NetWeightGrams;
        product.GrossWeightGrams   = dto.GrossWeightGrams;
        product.StoneWeightGrams   = dto.StoneWeightGrams;
        product.HallmarkNumber     = dto.HallmarkNumber?.Trim();
        product.MakingChargesType  = dto.MakingChargesType;
        product.MakingChargesValue = dto.MakingChargesValue;
        product.WastagePercent     = dto.WastagePercent;
        product.HallmarkCharges    = dto.HallmarkCharges;
        product.GSTRatePercent     = dto.GSTRatePercent;
        product.StoneValue         = dto.StoneValue;
        product.ReorderLevel       = dto.ReorderLevel;
        product.IsActive           = dto.IsActive;
        product.UpdatedAt          = DateTime.UtcNow;

        // Handle stock quantity changes through inventory adjustment
        if (product.StockQuantity != dto.StockQuantity)
        {
            var adjustment = dto.StockQuantity - product.StockQuantity;
            var transactionType = TransactionType.StockAdjustment;
            var reason = adjustment > 0 ? "Stock increase via product update" : "Stock decrease via product update";

            try
            {
                var price = await _priceService.GetCurrentRateAsync();
                if (price == null)
                    throw new InvalidOperationException("Unable to retrieve current gold prices for stock adjustment.");

                var rate = product.Purity.StartsWith("24") ? price.Rate24KPer10g / 10 : price.Rate22KPer10g / 10;

                await _invRepo.AddAsync(new InventoryTransaction
                {
                    ProductId       = product.Id,
                    TransactionType = transactionType,
                    Quantity        = Math.Abs(adjustment),
                    WeightGrams     = product.NetWeightGrams * Math.Abs(adjustment),
                    RatePerGram     = rate,
                    TotalValue      = product.NetWeightGrams * Math.Abs(adjustment) * rate,
                    ReferenceNo     = $"PRODUCT-UPDATE-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Notes           = reason,
                    CreatedByUserId = userId,
                    CreatedAt       = DateTime.UtcNow
                });
                await _invRepo.SaveAsync();

                product.StockQuantity = dto.StockQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stock adjustment failed during product update for product {ProductId}", product.Id);
                throw new InvalidOperationException("Failed to adjust stock quantity during product update.", ex);
            }
        }

        // Calculate gross weight if not provided or invalid
        if (product.GrossWeightGrams < product.NetWeightGrams + product.StoneWeightGrams)
        {
            product.GrossWeightGrams = product.NetWeightGrams + product.StoneWeightGrams;
        }

        if (!string.IsNullOrEmpty(dto.ImageBase64) && !string.IsNullOrEmpty(dto.ImageExtension))
        {
            try
            {
                product.ImageUrl = await _fileService.UploadBase64Async(
                    dto.ImageBase64, dto.ImageExtension, "products", dto.SKUCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image upload failed while updating product {ProductId}", product.Id);
                throw new InvalidOperationException("Image upload failed. Please try again.", ex);
            }
        }

        await _repo.UpdateAsync(product);
        await _repo.SaveAsync();
        return await EnrichWithPriceAsync(product);
    }

    public async Task<ProductResponseDto> GetByIdAsync(int id)
    {
        var p = await _repo.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Product {id} not found.");
        return await EnrichWithPriceAsync(p);
    }

    public async Task<ProductResponseDto?> GetBySkuAsync(string sku)
    {
        var p = await _repo.GetBySkuAsync(sku);
        return p is null ? null : await EnrichWithPriceAsync(p);
    }

    public async Task<(List<ProductSummaryDto> items, int total)> SearchAsync(ProductSearchDto filter)
    {
        var (products, total) = await _repo.SearchAsync(filter);
        var price = await _priceService.GetCurrentRateAsync();
        var summaries = products.Select(p => EnrichSummary(p, price)).ToList();
        return (summaries, total);
    }

    public async Task<List<ProductSummaryDto>> GetLowStockAsync()
    {
        var products = await _repo.GetLowStockAsync();
        var price    = await _priceService.GetCurrentRateAsync();
        return products.Select(p => EnrichSummary(p, price)).ToList();
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        var cats = await _repo.GetCategoriesAsync();
        return _mapper.Map<List<CategoryDto>>(cats);
    }

    public async Task SoftDeleteAsync(int id, string userId)
    {
        var p = await _repo.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Product {id} not found.");
        p.IsActive  = false;
        p.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(p);
        await _repo.SaveAsync();
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "PRODUCT_DELETE", EntityName = "Product",
            EntityId = id.ToString(), Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<string> GenerateQrCodeAsync(int productId)
    {
        var p = await _repo.GetByIdAsync(productId) ?? throw new KeyNotFoundException();
        return _barcodeService.GenerateProductQrCodeBase64(p.SKUCode, p.Name);
    }

    // ── Pricing enrichment ────────────────────────────────────────────────

    private async Task<ProductResponseDto> EnrichWithPriceAsync(Product p)
    {
        var dto   = _mapper.Map<ProductResponseDto>(p);
        var price = await _priceService.GetCurrentRateAsync();
        var rate  = p.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;

        var metalValue   = Math.Round(p.NetWeightGrams * rate, 2);
        var makingCharge = p.MakingChargesType == MakingChargesType.PerGram
            ? p.NetWeightGrams * p.MakingChargesValue
            : metalValue * (p.MakingChargesValue / 100m);
        var wastageVal   = metalValue * (p.WastagePercent / 100m);
        var subtotal     = metalValue + makingCharge + wastageVal + p.HallmarkCharges + p.StoneValue;
        var tax          = subtotal * (p.GSTRatePercent / 100m);

        return dto with
        {
            CurrentMetalValue    = metalValue,
            CurrentMakingCharges = Math.Round(makingCharge, 2),
            CurrentTotalValue    = Math.Round(subtotal + tax, 2)
        };
    }

    private static ProductSummaryDto EnrichSummary(Product p, GoldPriceDto price)
    {
        var rate = p.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;
        var val  = Math.Round(p.NetWeightGrams * rate, 2);
        return new ProductSummaryDto(p.Id, p.SKUCode, p.Name, p.Category?.Name ?? "",
            p.Purity, p.NetWeightGrams, p.StockQuantity,
            p.StockQuantity <= p.ReorderLevel, p.ImageUrl, val);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// INVENTORY SERVICE
// ─────────────────────────────────────────────────────────────────────────────

public interface IInventoryService
{
    Task<InventoryTransactionResponseDto> RecordStockInAsync(StockInDto dto, string userId);
    Task<InventoryTransactionResponseDto> RecordAdjustmentAsync(StockAdjustmentDto dto, string userId);
    Task<InventoryValuationDto> GetValuationAsync();
    Task<List<StockLevelDto>> GetStockLevelsAsync();
    Task<List<InventoryTransactionResponseDto>> GetTransactionsAsync(
        int page, int pageSize, DateTime? from, DateTime? to, string? type);
    Task RecordGoldLoanMovementAsync(int productId, TransactionType type,
        decimal weightGrams, string referenceNo, string userId);
}

public class InventoryService : IInventoryService
{
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _invRepo;
    private readonly IPriceService _priceService;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly ApplicationDbContext _db;

    public InventoryService(IProductRepository productRepo, IInventoryRepository invRepo,
        IPriceService priceService, IMapper mapper,
        INotificationService notifications, ApplicationDbContext db)
    {
        _productRepo = productRepo; _invRepo = invRepo;
        _priceService = priceService; _mapper = mapper;
        _notifications = notifications; _db = db;
    }

    public async Task<InventoryTransactionResponseDto> RecordStockInAsync(
        StockInDto dto, string userId)
    {
        var product = await _productRepo.GetByIdAsync(dto.ProductId)
            ?? throw new KeyNotFoundException($"Product {dto.ProductId} not found.");

        var txn = new InventoryTransaction
        {
            ProductId       = dto.ProductId,
            TransactionType = TransactionType.Purchase,
            Quantity        = dto.Quantity,
            WeightGrams     = dto.WeightGrams,
            RatePerGram     = dto.RatePerGram,
            TotalValue      = dto.WeightGrams * dto.RatePerGram,
            ReferenceNo     = dto.InvoiceNumber,
            Notes           = $"Supplier: {dto.SupplierName} | {dto.Notes}",
            CreatedByUserId = userId,
            CreatedAt       = DateTime.UtcNow
        };
        await _invRepo.AddAsync(txn);

        product.StockQuantity += dto.Quantity;
        product.UpdatedAt     = DateTime.UtcNow;
        await _productRepo.UpdateAsync(product);
        await _invRepo.SaveAsync();

        return _mapper.Map<InventoryTransactionResponseDto>(txn);
    }

    public async Task<InventoryTransactionResponseDto> RecordAdjustmentAsync(
        StockAdjustmentDto dto, string userId)
    {
        var product = await _productRepo.GetByIdAsync(dto.ProductId)
            ?? throw new KeyNotFoundException($"Product {dto.ProductId} not found.");

        var newQty = product.StockQuantity + dto.QuantityChange;
        if (newQty < 0)
            throw new InvalidOperationException(
                $"Adjustment would result in negative stock ({newQty}). Current: {product.StockQuantity}.");

        var price = await _priceService.GetCurrentRateAsync();
        var rate  = product.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;

        var txn = new InventoryTransaction
        {
            ProductId       = dto.ProductId,
            TransactionType = TransactionType.StockAdjustment,
            Quantity        = dto.QuantityChange,
            WeightGrams     = dto.WeightGramsChange,
            RatePerGram     = rate,
            TotalValue      = Math.Abs(dto.WeightGramsChange) * rate,
            Notes           = $"[{dto.ReasonCode}] {dto.Notes}",
            CreatedByUserId = userId,
            CreatedAt       = DateTime.UtcNow
        };
        await _invRepo.AddAsync(txn);

        product.StockQuantity = newQty;
        product.UpdatedAt     = DateTime.UtcNow;
        await _productRepo.UpdateAsync(product);
        await _invRepo.SaveAsync();

        // Low-stock alert
        if (product.StockQuantity <= product.ReorderLevel)
            await _notifications.NotifyAdminsAsync(
                $"Low Stock Alert: {product.Name}",
                $"Product {product.SKUCode} — {product.Name} has only " +
                $"{product.StockQuantity} pieces left (reorder level: {product.ReorderLevel}).");

        return _mapper.Map<InventoryTransactionResponseDto>(txn);
    }

    public async Task RecordGoldLoanMovementAsync(int productId, TransactionType type,
        decimal weightGrams, string referenceNo, string userId)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product is null) return;

        await _invRepo.AddAsync(new InventoryTransaction
        {
            ProductId       = productId,
            TransactionType = type,
            Quantity        = type == TransactionType.GoldLoanIn ? 1 : -1,
            WeightGrams     = weightGrams,
            RatePerGram     = 0,
            TotalValue      = 0,
            ReferenceNo     = referenceNo,
            Notes           = type == TransactionType.GoldLoanIn
                ? "Gold deposited as loan collateral"
                : "Gold returned to customer after loan repayment",
            CreatedByUserId = userId,
            CreatedAt       = DateTime.UtcNow
        });
        await _invRepo.SaveAsync();
    }

    public async Task<InventoryValuationDto> GetValuationAsync()
    {
        var price    = await _priceService.GetCurrentRateAsync();
        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity > 0)
            .ToListAsync();

        decimal goldWeight = 0, silverWeight = 0, goldValue = 0, silverValue = 0;
        var byCategory = products
            .GroupBy(p => p.Category.Name)
            .Select(g =>
            {
                var totalWeight = g.Sum(p => p.NetWeightGrams * p.StockQuantity);
                var isGold   = g.First().Category.MetalType == Domain.Enums.MetalType.Gold;
                var isSilver = g.First().Category.MetalType == Domain.Enums.MetalType.Silver;
                var rate     = isGold ? price.Rate22KPer10g / 10m :
                               isSilver ? price.SilverRatePerKg / 1000m : price.Rate22KPer10g / 10m;
                var value    = totalWeight * rate;

                if (isGold) { goldWeight += totalWeight; goldValue += value; }
                else if (isSilver) { silverWeight += totalWeight; silverValue += value; }

                return new CategoryValuationDto(
                    g.Key, g.Count(), g.Sum(p => p.StockQuantity),
                    Math.Round(totalWeight, 3), Math.Round(value, 2));
            }).ToList();

        return new InventoryValuationDto(DateTime.UtcNow,
            Math.Round(goldWeight, 3), Math.Round(silverWeight, 3),
            Math.Round(goldValue, 2), Math.Round(silverValue, 2),
            Math.Round(goldValue + silverValue, 2),
            price.Rate22KPer10g, price.Rate24KPer10g, price.SilverRatePerKg, byCategory);
    }

    public async Task<List<StockLevelDto>> GetStockLevelsAsync()
    {
        var price    = await _priceService.GetCurrentRateAsync();
        var products = await _db.Products.Include(p => p.Category)
            .Where(p => p.IsActive).ToListAsync();

        return products.Select(p =>
        {
            var rate  = p.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;
            var value = Math.Round(p.NetWeightGrams * p.StockQuantity * rate, 2);
            return new StockLevelDto(p.Id, p.SKUCode, p.Name, p.Category?.Name ?? "",
                p.Purity, p.StockQuantity, p.ReorderLevel,
                p.NetWeightGrams * p.StockQuantity, value, p.StockQuantity <= p.ReorderLevel);
        }).OrderBy(s => s.IsLowStock ? 0 : 1).ThenBy(s => s.ProductName).ToList();
    }

    public async Task<List<InventoryTransactionResponseDto>> GetTransactionsAsync(
        int page, int pageSize, DateTime? from, DateTime? to, string? type)
    {
        var txns = await _invRepo.GetAllAsync(page, pageSize, from, to, type);
        return _mapper.Map<List<InventoryTransactionResponseDto>>(txns);
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Repositories;
using ShreeJewellers.Infrastructure.Services;
using Xunit;

namespace ShreeJewellers.Tests.UnitTests;

public class InventoryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IPriceService>     _priceMock;
    private readonly Mock<INotificationService> _notifMock;
    private readonly Mock<IInventoryRepository> _invRepoMock;
    private readonly ProductRepository _productRepo;
    private readonly InventoryService _sut;

    private const string AdminUserId = "admin-001";

    public InventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        _priceMock   = new Mock<IPriceService>();
        _notifMock   = new Mock<INotificationService>();
        _invRepoMock = new Mock<IInventoryRepository>();
        _productRepo = new ProductRepository(_db);

        _sut = new InventoryService(
            _productRepo, _invRepoMock.Object, _priceMock.Object,
            null!, _notifMock.Object, _db);

        SeedDatabase();
    }

    private void SeedDatabase()
    {
        _db.Categories.Add(new Category { Id = 1, Name = "Gold Jewellery", MetalType = MetalType.Gold, IsActive = true, CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();

        // Normal stock product
        _db.Products.Add(new Product
        {
            Id = 1, CategoryId = 1, Name = "Gold Ring 22K", SKUCode = "GLD-RING-001",
            HSNCode = "7113", Purity = "22K", NetWeightGrams = 5m, GrossWeightGrams = 5.5m,
            MakingChargesType = MakingChargesType.PerGram, MakingChargesValue = 200m,
            GSTRatePercent = 3m, StockQuantity = 10, ReorderLevel = 3,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });

        // Low-stock product (at reorder level)
        _db.Products.Add(new Product
        {
            Id = 2, CategoryId = 1, Name = "Gold Bangle 22K", SKUCode = "GLD-BANGLE-001",
            HSNCode = "7113", Purity = "22K", NetWeightGrams = 15m, GrossWeightGrams = 16m,
            MakingChargesType = MakingChargesType.PerGram, MakingChargesValue = 150m,
            GSTRatePercent = 3m, StockQuantity = 3, ReorderLevel = 3,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });

        // Near-zero stock
        _db.Products.Add(new Product
        {
            Id = 3, CategoryId = 1, Name = "Gold Chain 22K", SKUCode = "GLD-CHAIN-001",
            HSNCode = "7113", Purity = "22K", NetWeightGrams = 8m, GrossWeightGrams = 8.5m,
            MakingChargesType = MakingChargesType.PerGram, MakingChargesValue = 180m,
            GSTRatePercent = 3m, StockQuantity = 1, ReorderLevel = 2,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });

        _db.SaveChanges();

        _priceMock.Setup(p => p.GetCurrentRateAsync())
            .ReturnsAsync(new GoldPriceDto(6500m, 7100m, 95000m, DateTime.UtcNow, false));
        _notifMock.Setup(n => n.NotifyAdminsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _invRepoMock.Setup(r => r.AddAsync(It.IsAny<InventoryTransaction>()))
            .Returns(Task.CompletedTask);
        _invRepoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
    }

    // ═══════════════════════════════════════════════════════
    // STOCK IN TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task RecordStockIn_ValidInput_IncreasesStockQuantity()
    {
        // Arrange
        var dto = new StockInDto(
            ProductId: 1, Quantity: 5, WeightGrams: 25m, RatePerGram: 600m,
            SupplierName: "Mehta Suppliers", InvoiceNumber: "INV-001",
            PurchaseDate: DateOnly.FromDateTime(DateTime.Today), Notes: "Regular stock");

        // Act
        await _sut.RecordStockInAsync(dto, AdminUserId);

        // Assert
        var product = await _db.Products.FindAsync(1);
        product!.StockQuantity.Should().Be(15); // was 10, added 5
    }

    [Fact]
    public async Task RecordStockIn_CreatesInventoryTransaction()
    {
        var dto = new StockInDto(1, 3, 15m, 650m, null, null, DateOnly.FromDateTime(DateTime.Today), null);

        await _sut.RecordStockInAsync(dto, AdminUserId);

        _invRepoMock.Verify(r => r.AddAsync(It.Is<InventoryTransaction>(t =>
            t.ProductId == 1 &&
            t.TransactionType == TransactionType.Purchase &&
            t.Quantity == 3 &&
            t.WeightGrams == 15m &&
            t.RatePerGram == 650m)), Times.Once);
    }

    [Fact]
    public async Task RecordStockIn_NonExistentProduct_ThrowsKeyNotFoundException()
    {
        var dto = new StockInDto(999, 1, 5m, 600m, null, null, DateOnly.FromDateTime(DateTime.Today), null);

        var act = async () => await _sut.RecordStockInAsync(dto, AdminUserId);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    // ═══════════════════════════════════════════════════════
    // STOCK ADJUSTMENT TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task RecordAdjustment_NegativeAdjustment_DeductsStock()
    {
        var dto = new StockAdjustmentDto(
            ProductId: 1, QuantityChange: -2, WeightGramsChange: -10m,
            ReasonCode: "DAMAGE", Notes: "2 rings damaged in display");

        await _sut.RecordAdjustmentAsync(dto, AdminUserId);

        var product = await _db.Products.FindAsync(1);
        product!.StockQuantity.Should().Be(8); // 10 - 2
    }

    [Fact]
    public async Task RecordAdjustment_WouldResultInNegativeStock_Throws()
    {
        var dto = new StockAdjustmentDto(1, -15, -75m, "LOST", "Theft"); // Stock is 10, deducting 15

        var act = async () => await _sut.RecordAdjustmentAsync(dto, AdminUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*negative stock*");
    }

    [Fact]
    public async Task RecordAdjustment_BelowReorderLevel_SendsLowStockAlert()
    {
        // Product 1 has stock=10, reorder=3. Adjust by -8 → stock becomes 2 (below reorder)
        var dto = new StockAdjustmentDto(1, -8, -40m, "DAMAGE", "8 pieces damaged");

        await _sut.RecordAdjustmentAsync(dto, AdminUserId);

        _notifMock.Verify(n => n.NotifyAdminsAsync(
            It.Is<string>(s => s.Contains("Low Stock") || s.Contains("low stock")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RecordAdjustment_AtReorderLevel_AlsoSendsAlert()
    {
        // Product 1 (stock=10, reorder=3). Deduct 7 → stock=3 = exactly at reorder level
        var dto = new StockAdjustmentDto(1, -7, -35m, "COUNT_CORRECTION", "Physical count reconciliation");

        await _sut.RecordAdjustmentAsync(dto, AdminUserId);

        var product = await _db.Products.FindAsync(1);
        product!.StockQuantity.Should().Be(3);

        _notifMock.Verify(n => n.NotifyAdminsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RecordAdjustment_StillAboveReorderLevel_NoAlert()
    {
        // Stock=10, reorder=3. Adjust by -2 → stock=8 (still well above)
        var dto = new StockAdjustmentDto(1, -2, -10m, "DAMAGE", "Minor damage");

        await _sut.RecordAdjustmentAsync(dto, AdminUserId);

        _notifMock.Verify(n => n.NotifyAdminsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════
    // STOCK LEVEL TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetStockLevels_IncludesLowStockFlag()
    {
        var levels = await _sut.GetStockLevelsAsync();

        // Product 2 (stock=3, reorder=3) should be flagged as low stock
        var lowStockProduct = levels.FirstOrDefault(l => l.ProductId == 2);
        lowStockProduct.Should().NotBeNull();
        lowStockProduct!.IsLowStock.Should().BeTrue();

        // Product 1 (stock=10, reorder=3) should NOT be flagged
        var normalProduct = levels.FirstOrDefault(l => l.ProductId == 1);
        normalProduct!.IsLowStock.Should().BeFalse();
    }

    [Fact]
    public async Task GetStockLevels_LowStockProductsFirst()
    {
        var levels = await _sut.GetStockLevelsAsync();

        // Low stock items should appear before normal stock
        var firstItem = levels.First();
        firstItem.IsLowStock.Should().BeTrue("low-stock products should be sorted first");
    }

    [Fact]
    public async Task GetValuation_CalculatesCorrectValue()
    {
        // Products: 1 (10 units × 5g @ 650/g), 2 (3 units × 15g), 3 (1 unit × 8g)
        // Total gold weight = (10×5) + (3×15) + (1×8) = 50 + 45 + 8 = 103g
        // Value = 103 × 650 = ₹66,950 (approx, depends on rate mock)

        var valuation = await _sut.GetValuationAsync();

        valuation.Should().NotBeNull();
        valuation.TotalGoldWeightGrams.Should().BeGreaterThan(0);
        valuation.TotalInventoryValue.Should().BeGreaterThan(0);
        valuation.ByCategory.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}

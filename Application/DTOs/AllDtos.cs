using ShreeJewellers.Domain.Enums;

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCT DTOs
// ─────────────────────────────────────────────────────────────────────────────

namespace ShreeJewelers.Application.DTOs
{
    // Product DTOs
    public record CreateProductDto(
        int CategoryId,
        string Name,
        string? Description,
        string SKUCode,
        string HSNCode,
        string Purity,
        decimal NetWeightGrams,
        decimal GrossWeightGrams,
        decimal StoneWeightGrams,
        string? HallmarkNumber,
        MakingChargesType MakingChargesType,
        decimal MakingChargesValue,
        decimal WastagePercent,
        decimal HallmarkCharges,
        decimal GSTRatePercent,
        decimal StoneValue,
        int StockQuantity,
        int ReorderLevel,
        string? ImageBase64,
        string? ImageExtension
    );

    public record UpdateProductDto(
        int Id,
        int CategoryId,
        string Name,
        string? Description,
        string SKUCode,
        string HSNCode,
        string Purity,
        decimal NetWeightGrams,
        decimal GrossWeightGrams,
        decimal StoneWeightGrams,
        string? HallmarkNumber,
        MakingChargesType MakingChargesType,
        decimal MakingChargesValue,
        decimal WastagePercent,
        decimal HallmarkCharges,
        decimal GSTRatePercent,
        decimal StoneValue,
        int StockQuantity,
        int ReorderLevel,
        bool IsActive,
        string? ImageBase64,
        string? ImageExtension
    );

    public record ProductResponseDto(
        int Id,
        int CategoryId,
        string CategoryName,
        string Name,
        string? Description,
        string SKUCode,
        string HSNCode,
        string Purity,
        decimal NetWeightGrams,
        decimal GrossWeightGrams,
        decimal StoneWeightGrams,
        string? HallmarkNumber,
        string MakingChargesType,
        decimal MakingChargesValue,
        decimal WastagePercent,
        decimal HallmarkCharges,
        decimal GSTRatePercent,
        decimal StoneValue,
        int StockQuantity,
        int ReorderLevel,
        bool IsLowStock,
        string? ImageUrl,
        string? BarcodeData,
        bool IsActive,
        DateTime CreatedAt,
        decimal? CurrentMetalValue,
        decimal? CurrentMakingCharges,
        decimal? CurrentTotalValue
    );

    public record ProductSummaryDto(
        int Id,
        string SKUCode,
        string Name,
        string CategoryName,
        string Purity,
        decimal NetWeightGrams,
        int StockQuantity,
        bool IsLowStock,
        string? ImageUrl,
        decimal? CurrentTotalValue
    );

    public record ProductSearchDto(
        string? Query,
        int? CategoryId,
        string? Purity,
        bool? InStockOnly,
        bool? LowStockOnly,
        int Page = 1,
        int PageSize = 20
    );

    public record CategoryDto(
        int Id,
        string Name,
        string? Description,
        string MetalType,
        int ProductCount,
        bool IsActive
    );

    // Inventory DTOs
    public record StockInDto(
        int ProductId,
        int Quantity,
        decimal WeightGrams,
        decimal RatePerGram,
        string? SupplierName,
        string? InvoiceNumber,
        DateOnly PurchaseDate,
        string? Notes
    );

    public record StockAdjustmentDto(
        int ProductId,
        int QuantityChange,
        decimal WeightGramsChange,
        string ReasonCode,
        string Notes
    );

    public record InventoryTransactionResponseDto(
        int Id = 0,
        int ProductId = 0,
        string ProductName = "",
        string SKUCode = "",
        string TransactionType = "",
        int Quantity = 0,
        decimal WeightGrams = 0,
        decimal RatePerGram = 0,
        decimal TotalValue = 0,
        string? ReferenceNo = null,
        string? Notes = null,
        string CreatedByName = "",
        DateTime CreatedAt = default
    );

    public record StockLevelDto(
        int ProductId,
        string SKUCode,
        string ProductName,
        string CategoryName,
        string Purity,
        int CurrentStock,
        int ReorderLevel,
        decimal TotalWeightGrams,
        decimal CurrentMarketValue,
        bool IsLowStock
    );

    public record InventoryValuationDto(
        DateTime AsOf,
        decimal TotalGoldWeightGrams,
        decimal TotalSilverWeightGrams,
        decimal TotalGoldValueINR,
        decimal TotalSilverValueINR,
        decimal TotalInventoryValue,
        decimal GoldRate22K,
        decimal GoldRate24K,
        decimal SilverRatePerKg,
        List<CategoryValuationDto> ByCategory
    );

    public record CategoryValuationDto(
        string CategoryName,
        int ProductCount,
        int TotalPieces,
        decimal TotalWeightGrams,
        decimal TotalValue
    );

    public record GoldPriceResponseDto(
        decimal Rate22KPer10g,
        decimal Rate24KPer10g,
        decimal SilverRatePerKg,
        decimal Rate22KPerGram,
        decimal Rate24KPerGram,
        decimal SilverRatePerGram,
        DateTime RecordedAt,
        bool IsManualOverride,
        string Source
    );

    public record ManualPriceOverrideDto(
        decimal? GoldRate22KPer10g,
        decimal? GoldRate24KPer10g,
        decimal? SilverRatePerKg,
        string Reason
    );

    // Sales DTOs
    public record CreateSalesOrderDto(
        string? CustomerUserId,
        string? WalkInCustomerName,
        string? WalkInCustomerPhone,
        DateOnly OrderDate,
        List<CreateSalesOrderItemDto> Items,
        string PaymentMode,
        decimal AmountPaid,
        decimal AdvanceAmount,
        decimal DiscountAmount,
        decimal OldGoldExchangeValue,
        decimal OldGoldWeightGrams,
        string? OldGoldPurity,
        bool IsInterState,
        string? Notes
    );

    public record UpdateSalesOrderDto(
        int Id,
        string? CustomerUserId,
        string? WalkInCustomerName,
        string? WalkInCustomerPhone,
        DateOnly OrderDate,
        string PaymentMode,
        string PaymentStatus,
        string Status,
        decimal AmountPaid,
        decimal AdvanceAmount,
        decimal DiscountAmount,
        decimal OldGoldExchangeValue,
        decimal OldGoldWeightGrams,
        string? OldGoldPurity,
        bool IsInterState,
        string? Notes,
        List<CreateSalesOrderItemDto>? Items
    );

    public record CreateSalesOrderItemDto(
        int ProductId,
        int Quantity,
        decimal WeightGrams,
        decimal RatePerGram,
        decimal MakingCharges,
        decimal HallmarkCharges,
        decimal StoneValue,
        decimal DiscountAmount
    );

    public record SalesOrderResponseDto(
        int Id,
        string OrderNumber,
        string? CustomerUserId,
        string? CustomerName,
        string? CustomerCode,
        string? WalkInCustomerName,
        string? WalkInCustomerPhone,
        DateOnly OrderDate,
        decimal GrossAmount,
        decimal DiscountAmount,
        decimal OldGoldExchangeValue,
        decimal TaxAmount,
        decimal CGSTAmount,
        decimal SGSTAmount,
        decimal IGSTAmount,
        decimal NetAmount,
        decimal AmountPaid,
        decimal AdvanceAmount,
        decimal BalanceDue,
        string PaymentMode,
        string PaymentStatus,
        string Status,
        string? InvoiceUrl,
        string? Notes,
        List<SalesOrderItemResponseDto> Items,
        DateTime CreatedAt
    );

    public record SalesOrderItemResponseDto(
        int Id,
        int ProductId,
        string ProductName,
        string SKUCode,
        string Purity,
        int Quantity,
        decimal WeightGrams,
        decimal RatePerGram,
        decimal MakingCharges,
        decimal HallmarkCharges,
        decimal StoneValue,
        decimal TaxPercent,
        decimal TaxAmount,
        decimal DiscountAmount,
        decimal LineTotal
    );

    public record SalesOrderSummaryDto(
        int Id,
        string OrderNumber,
        string? CustomerName,
        DateOnly OrderDate,
        decimal NetAmount,
        string PaymentStatus,
        string Status,
        bool HasInvoice
    );

    public record ReturnOrderDto(
        int OriginalOrderId,
        List<int> ItemIdsToReturn,
        string ReturnReason,
        string RefundMode,
        string? Notes
    );

    public record SalesFilterDto(
        DateOnly? FromDate,
        DateOnly? ToDate,
        string? CustomerSearch,
        string? Status,
        string? PaymentStatus,
        int Page = 1,
        int PageSize = 20
    );

    public record DailySalesSummaryDto(
        DateOnly Date,
        int OrderCount,
        decimal TotalGrossAmount,
        decimal TotalDiscounts,
        decimal TotalTax,
        decimal TotalNetAmount,
        decimal TotalAmountReceived,
        Dictionary<string, decimal> ByPaymentMode,
        Dictionary<string, decimal> ByCategoryName
    );
}

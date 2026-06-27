using AutoMapper;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Domain.Entities;

namespace ShreeJewellers.Application.Mappings;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // ── Category ──────────────────────────────────────────────────────
        CreateMap<Category, CategoryDto>()
            .ConstructUsing(src => new CategoryDto(
                src.Id,
                src.Name,
                src.Description,
                src.MetalType.ToString(),
                src.Products.Count,
                src.IsActive
            ));

        // ── Product ───────────────────────────────────────────────────────
        CreateMap<Product, ProductResponseDto>()
            .ConstructUsing(src => new ProductResponseDto(
                src.Id,
                src.CategoryId,
                src.Category != null ? src.Category.Name : "",
                src.Name,
                src.Description,
                src.SKUCode,
                src.HSNCode,
                src.Purity,
                src.NetWeightGrams,
                src.GrossWeightGrams,
                src.StoneWeightGrams,
                src.HallmarkNumber,
                src.MakingChargesType.ToString(),
                src.MakingChargesValue,
                src.WastagePercent,
                src.HallmarkCharges,
                src.GSTRatePercent,
                src.StoneValue,
                src.StockQuantity,
                src.ReorderLevel,
                src.StockQuantity <= src.ReorderLevel,
                src.ImageUrl,
                src.BarcodeData,
                src.IsActive,
                src.CreatedAt,
                null, // CurrentMetalValue (set by EnrichWithPriceAsync)
                null, // CurrentMakingCharges
                null  // CurrentTotalValue
            ));

        //.ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name))
        //    .ForMember(d => d.MakingChargesType, o => o.MapFrom(s => s.MakingChargesType.ToString()))
        //    .ForMember(d => d.IsLowStock, o => o.MapFrom(s => s.StockQuantity <= s.ReorderLevel))
        //    // Live price fields are populated by service, not mapper
        //    .ForMember(d => d.CurrentMetalValue, o => o.Ignore())
        //    .ForMember(d => d.CurrentMakingCharges, o => o.Ignore())
        //    .ForMember(d => d.CurrentTotalValue, o => o.Ignore());

        CreateMap<Product, ProductSummaryDto>()
            .ConstructUsing(src => new ProductSummaryDto(
                src.Id,
                src.SKUCode,
                src.Name,
                src.Category != null ? src.Category.Name : "",
                src.Purity,
                src.NetWeightGrams,
                src.StockQuantity,
                src.StockQuantity <= src.ReorderLevel,
                src.ImageUrl,
                null
            ));

        CreateMap<DateTime, DateOnly>().ConvertUsing(src => DateOnly.FromDateTime(src));
        CreateMap<DateTime?, DateOnly?>().ConvertUsing(src => src.HasValue ? DateOnly.FromDateTime(src.Value) : null);

        // ── InventoryTransaction ──────────────────────────────────────────
        CreateMap<InventoryTransaction, InventoryTransactionResponseDto>()
            .ConstructUsing(src => new InventoryTransactionResponseDto(
                src.Id,
                src.ProductId,
                src.Product != null ? src.Product.Name : "",
                src.Product != null ? src.Product.SKUCode : "",
                src.TransactionType.ToString(),
                src.Quantity,
                src.WeightGrams,
                src.RatePerGram,
                src.TotalValue,
                src.ReferenceNo,
                src.Notes,
                src.CreatedBy != null ? $"{src.CreatedBy.FirstName} {src.CreatedBy.LastName}".Trim() : "",
                src.CreatedAt
            ));

        // ── SalesOrder ────────────────────────────────────────────────────
        CreateMap<SalesOrder, SalesOrderResponseDto>()
            .ConstructUsing(src => new SalesOrderResponseDto(
                src.Id,
                src.OrderNumber,
                src.CustomerUserId,
                src.Customer != null ? $"{src.Customer.FirstName} {src.Customer.LastName}".Trim() : null,
                src.Customer != null ? src.Customer.CustomerCode : null,
                null,
                null,
                DateOnly.FromDateTime(src.OrderDate),
                src.GrossAmount,
                src.DiscountAmount,
                src.OldGoldExchangeValue,
                src.TaxAmount,
                src.CGSTAmount,
                src.SGSTAmount,
                src.IGSTAmount,
                src.NetAmount,
                src.AmountPaid,
                src.AdvanceAmount,
                src.NetAmount - src.AmountPaid,
                src.PaymentMode.ToString(),
                src.PaymentStatus.ToString(),
                src.Status.ToString(),
                src.InvoiceUrl,
                src.Notes,
                src.Items != null ? src.Items.Select(item => new SalesOrderItemResponseDto(
                    item.Id,
                    item.ProductId,
                    item.Product != null ? item.Product.Name : "",
                    item.Product != null ? item.Product.SKUCode : "",
                    item.Product != null ? item.Product.Purity : "",
                    item.Quantity,
                    item.WeightGrams,
                    item.RatePerGram,
                    item.MakingCharges,
                    item.HallmarkCharges,
                    item.StoneValue,
                    item.TaxPercent,
                    item.TaxAmount,
                    item.DiscountAmount,
                    item.LineTotal
                )).ToList() : new List<SalesOrderItemResponseDto>(),
                src.CreatedAt
            ));

        CreateMap<SalesOrder, SalesOrderSummaryDto>()
            .ConstructUsing(src => new SalesOrderSummaryDto(
                src.Id,
                src.OrderNumber,
                src.Customer != null ? $"{src.Customer.FirstName} {src.Customer.LastName}".Trim() : "Walk-in",
                DateOnly.FromDateTime(src.OrderDate),
                src.NetAmount,
                src.PaymentStatus.ToString(),
                src.Status.ToString(),
                src.InvoiceUrl != null
            ));

        CreateMap<SalesOrderItem, SalesOrderItemResponseDto>()
            .ConstructUsing(src => new SalesOrderItemResponseDto(
                src.Id,
                src.ProductId,
                src.Product != null ? src.Product.Name : "",
                src.Product != null ? src.Product.SKUCode : "",
                src.Product != null ? src.Product.Purity : "",
                src.Quantity,
                src.WeightGrams,
                src.RatePerGram,
                src.MakingCharges,
                src.HallmarkCharges,
                src.StoneValue,
                src.TaxPercent,
                src.TaxAmount,
                src.DiscountAmount,
                src.LineTotal
            ));
    }
}

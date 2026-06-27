using FluentValidation;
using ShreeJewelers.Application.DTOs;

namespace ShreeJewellers.Application.Validators;

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCT VALIDATORS
// ─────────────────────────────────────────────────────────────────────────────

public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    private static readonly string[] ValidPurities =
        { "24K", "22K", "18K", "14K", "925", "999", "92.5", "Other" };

    public CreateProductValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Category is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SKUCode).NotEmpty().MaximumLength(50)
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must contain only uppercase letters, numbers, and hyphens.");
        RuleFor(x => x.HSNCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Purity).NotEmpty()
            .Must(p => ValidPurities.Any(vp => p.StartsWith(vp, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"Purity must be one of: {string.Join(", ", ValidPurities)}");
        RuleFor(x => x.NetWeightGrams).GreaterThan(0).WithMessage("Net weight must be greater than zero.");
        RuleFor(x => x.GrossWeightGrams).GreaterThanOrEqualTo(0)
            .WithMessage("Gross weight must be ≥ 0.");
        RuleFor(x => x.StoneWeightGrams).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MakingChargesValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WastagePercent).InclusiveBetween(0, 20)
            .WithMessage("Wastage percent must be between 0 and 20.");
        RuleFor(x => x.HallmarkCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GSTRatePercent).InclusiveBetween(0, 28)
            .WithMessage("GST rate must be between 0 and 28.");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageExtension)
            .Must(ext => string.IsNullOrEmpty(ext) ||
                         new[] { "jpg", "jpeg", "png" }.Contains(ext.ToLower()))
            .WithMessage("Image must be jpg, jpeg, or png.")
            .When(x => !string.IsNullOrEmpty(x.ImageBase64));
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SKUCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NetWeightGrams).GreaterThan(0);
        RuleFor(x => x.GrossWeightGrams).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WastagePercent).InclusiveBetween(0, 20);
        RuleFor(x => x.GSTRatePercent).InclusiveBetween(0, 28);
        RuleFor(x => x.ImageExtension)
            .Must(ext => string.IsNullOrEmpty(ext) ||
                         new[] { "jpg", "jpeg", "png" }.Contains(ext.ToLower()))
            .WithMessage("Image must be jpg, jpeg, or png.")
            .When(x => !string.IsNullOrEmpty(x.ImageBase64));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// INVENTORY VALIDATORS
// ─────────────────────────────────────────────────────────────────────────────

public class StockInValidator : AbstractValidator<StockInDto>
{
    private static readonly string[] ValidReasonCodes =
        { "DAMAGE", "LOST", "COUNT_CORRECTION", "RETURN_TO_SUPPLIER", "DISPLAY_SAMPLE" };

    public StockInValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Product is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
        RuleFor(x => x.WeightGrams).GreaterThan(0).WithMessage("Weight must be greater than zero.");
        RuleFor(x => x.RatePerGram).GreaterThan(0).WithMessage("Purchase rate is required.");
        RuleFor(x => x.PurchaseDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Purchase date cannot be in the future.");
        RuleFor(x => x.InvoiceNumber).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.InvoiceNumber));
        RuleFor(x => x.SupplierName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.SupplierName));
    }
}

public class StockAdjustmentValidator : AbstractValidator<StockAdjustmentDto>
{
    private static readonly string[] ValidReasonCodes =
        { "DAMAGE", "LOST", "COUNT_CORRECTION", "RETURN_TO_SUPPLIER", "DISPLAY_SAMPLE", "MELTED" };

    public StockAdjustmentValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.QuantityChange).NotEqual(0).WithMessage("Quantity change cannot be zero.");
        RuleFor(x => x.ReasonCode).NotEmpty()
            .Must(r => ValidReasonCodes.Contains(r))
            .WithMessage($"Reason must be one of: {string.Join(", ", ValidReasonCodes)}");
        RuleFor(x => x.Notes).NotEmpty().WithMessage("Adjustment notes are mandatory.")
            .MaximumLength(500);
    }
}

public class ManualPriceOverrideValidator : AbstractValidator<ManualPriceOverrideDto>
{
    public ManualPriceOverrideValidator()
    {
        RuleFor(x => x)
            .Must(x => x.GoldRate22KPer10g.HasValue || x.GoldRate24KPer10g.HasValue || x.SilverRatePerKg.HasValue)
            .WithMessage("At least one price must be provided for override.");
        RuleFor(x => x.GoldRate22KPer10g).GreaterThan(0).When(x => x.GoldRate22KPer10g.HasValue);
        RuleFor(x => x.GoldRate24KPer10g).GreaterThan(0).When(x => x.GoldRate24KPer10g.HasValue);
        RuleFor(x => x.SilverRatePerKg).GreaterThan(0).When(x => x.SilverRatePerKg.HasValue);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SALES ORDER VALIDATORS
// ─────────────────────────────────────────────────────────────────────────────

public class CreateSalesOrderValidator : AbstractValidator<CreateSalesOrderDto>
{
    private static readonly string[] ValidPaymentModes =
        { "Cash", "Card", "UPI", "NEFT", "Cheque", "Split" };

    public CreateSalesOrderValidator()
    {
        // Either a registered customer OR walk-in details required
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.CustomerUserId) ||
                       !string.IsNullOrEmpty(x.WalkInCustomerName))
            .WithMessage("Either a customer account or walk-in customer name is required.");

        RuleFor(x => x.WalkInCustomerPhone)
            .Matches(@"^[6-9]\d{9}$").WithMessage("Enter a valid 10-digit mobile number.")
            .When(x => !string.IsNullOrEmpty(x.WalkInCustomerPhone));

        RuleFor(x => x.OrderDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Order date cannot be in the future.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required.")
            .Must(items => items.Count <= 50).WithMessage("Maximum 50 line items per invoice.");

        RuleForEach(x => x.Items).SetValidator(new SalesOrderItemValidator());

        RuleFor(x => x.PaymentMode)
            .NotEmpty()
            .Must(m => ValidPaymentModes.Contains(m))
            .WithMessage($"Payment mode must be: {string.Join(", ", ValidPaymentModes)}");

        RuleFor(x => x.AmountPaid).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OldGoldExchangeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OldGoldWeightGrams)
            .GreaterThan(0).When(x => x.OldGoldExchangeValue > 0)
            .WithMessage("Gold weight required when exchange value is provided.");
    }
}

public class SalesOrderItemValidator : AbstractValidator<CreateSalesOrderItemDto>
{
    public SalesOrderItemValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Product is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
        RuleFor(x => x.WeightGrams).GreaterThan(0).WithMessage("Weight must be greater than zero.");
        RuleFor(x => x.RatePerGram).GreaterThan(0).WithMessage("Rate per gram is required.");
        RuleFor(x => x.MakingCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HallmarkCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StoneValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
    }
}

public class ReturnOrderValidator : AbstractValidator<ReturnOrderDto>
{
    private static readonly string[] ValidRefundModes = { "Cash", "Card", "UPI", "NEFT", "StoreCredit" };

    public ReturnOrderValidator()
    {
        RuleFor(x => x.OriginalOrderId).GreaterThan(0);
        RuleFor(x => x.ItemIdsToReturn)
            .NotEmpty().WithMessage("At least one item must be selected for return.");
        RuleFor(x => x.ReturnReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.RefundMode)
            .Must(m => ValidRefundModes.Contains(m))
            .WithMessage($"Refund mode must be: {string.Join(", ", ValidRefundModes)}");
    }
}

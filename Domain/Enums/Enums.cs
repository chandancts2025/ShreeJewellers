namespace ShreeJewellers.Domain.Enums;

public enum KYCStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2
}

public enum LoanStatus
{
    Active = 0,
    PartiallyRepaid = 1,
    Closed = 2,
    Defaulted = 3,
    Extended = 4
}

public enum TransactionType
{
    Purchase = 0,
    Sale = 1,
    Return = 2,
    GoldLoanIn = 3,      // Gold deposited as collateral
    GoldLoanOut = 4,     // Gold returned to customer after repayment
    StockAdjustment = 5
}

public enum PaymentMode
{
    Cash = 0,
    Card = 1,
    UPI = 2,
    NEFT = 3,
    Cheque = 4,
    Split = 5            // Multiple payment modes in one transaction
}

public enum PaymentStatus
{
    Pending = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Refunded = 3
}

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Delivered = 2,
    Cancelled = 3,
    Returned = 4
}

public enum InterestCalculationType
{
    Daily = 0,
    Monthly = 1,
    Yearly = 2
}

public enum NotificationType
{
    SMS = 0,
    Email = 1,
    InApp = 2
}

public enum MetalType
{
    Gold = 0,
    Silver = 1,
    Diamond = 2,
    Platinum = 3,
    Other = 4
}

public enum MakingChargesType
{
    PerGram = 0,
    Percentage = 1,
    Fixed = 2
}

public enum PriceSource
{
    API = 0,
    Manual = 1,
    Opening = 2
}

namespace ShreeJewellers.Application.DTOs.Reports;

// ─────────────────────────────────────────────────────────────────────────────
// COMMON FILTER
// ─────────────────────────────────────────────────────────────────────────────

public record ReportFilterDto(
    DateOnly? DateFrom      = null,
    DateOnly? DateTo        = null,
    string?   CustomerId    = null,
    int?      CategoryId    = null,
    string?   ReportType    = null,
    string    ExportFormat  = "json",    // "json" | "xlsx" | "pdf"
    int       SlowMovingDays = 90,
    bool      IncludeInactive = false,
    int       Page           = 1,
    int       PageSize       = 200
);

// ─────────────────────────────────────────────────────────────────────────────
// DASHBOARD
// ─────────────────────────────────────────────────────────────────────────────

public record DashboardDto(
    // Today's Sales
    decimal TodaySalesAmount,
    int     TodaySalesOrderCount,
    decimal TodaySalesWeightGrams,

    // Gold Loans
    int     ActiveLoanCount,
    decimal ActiveLoanPrincipalTotal,
    decimal MonthlyInterestAccrued,
    int     OverdueLoanCount,
    int     LoansDueThisWeekCount,
    decimal DefaultedLoanPrincipal,

    // Inventory
    decimal StockValueAtLivePrice,
    int     LowStockProductCount,
    decimal GoldWeightInStockGrams,
    decimal SilverWeightInStockGrams,

    // Customers
    int NewCustomersThisMonth,
    int PendingKYCCount,

    // Live Prices
    decimal GoldRate22KPer10g,
    decimal GoldRate24KPer10g,
    decimal SilverRatePerKg,
    DateTime PriceLastUpdated,
    bool     PriceIsManualOverride,

    // Charts data
    List<MonthlySalesChartPoint>    SalesTrend12Months,
    List<CategorySalesChartPoint>   SalesByCategory,
    List<MonthlyInterestChartPoint> InterestIncome6Months
);

public record MonthlySalesChartPoint(string Month, decimal Amount, int OrderCount);
public record CategorySalesChartPoint(string Category, decimal Amount, decimal Percent);
public record MonthlyInterestChartPoint(string Month, decimal Interest, int ActiveLoans);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 1 — Sales Summary
// ─────────────────────────────────────────────────────────────────────────────

public record SalesSummaryReportDto(
    DateOnly        FromDate,
    DateOnly        ToDate,
    int             TotalOrders,
    decimal         TotalGrossAmount,
    decimal         TotalDiscount,
    decimal         TotalTax,
    decimal         TotalOldGoldExchange,
    decimal         TotalNetAmount,
    decimal         TotalAmountReceived,
    decimal         TotalBalanceDue,
    List<SalesByPeriodDto>      ByPeriod,
    List<SalesByCategoryDto>    ByCategory,
    List<SalesByPaymentModeDto> ByPaymentMode
);

public record SalesByPeriodDto(
    string  Period,          // "2024-01-15" or "2024-01" or "2024"
    int     Orders,
    decimal GrossAmount,
    decimal NetAmount,
    decimal TaxAmount
);

public record SalesByCategoryDto(
    string  CategoryName,
    int     Orders,
    int     ItemsSold,
    decimal TotalWeightGrams,
    decimal TotalNetAmount,
    decimal Percent
);

public record SalesByPaymentModeDto(
    string  PaymentMode,
    int     Count,
    decimal Amount,
    decimal Percent
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 2 — Product-wise Sales
// ─────────────────────────────────────────────────────────────────────────────

public record ProductSalesReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    List<ProductSalesRowDto> Rows,
    decimal TotalRevenue,
    decimal TotalWeightSold
);

public record ProductSalesRowDto(
    int     ProductId,
    string  SKUCode,
    string  ProductName,
    string  CategoryName,
    string  Purity,
    int     QuantitySold,
    decimal WeightSoldGrams,
    decimal TotalRevenue,
    decimal TotalMakingCharges,
    decimal TotalTax,
    decimal EstimatedCOGS,       // Weight × avg purchase rate
    decimal EstimatedMargin,
    decimal MarginPercent
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 3 — Customer Purchase History
// ─────────────────────────────────────────────────────────────────────────────

public record CustomerPurchaseHistoryDto(
    string  CustomerId,
    string  CustomerName,
    string  CustomerCode,
    string  Phone,
    int     TotalOrders,
    decimal TotalSpent,
    decimal TotalWeightPurchasedGrams,
    int     LoyaltyPoints,
    List<CustomerOrderSummaryDto> Orders
);

public record CustomerOrderSummaryDto(
    int     OrderId,
    string  OrderNumber,
    DateOnly OrderDate,
    decimal NetAmount,
    string  PaymentStatus,
    string  Status
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 4 — GST Report
// ─────────────────────────────────────────────────────────────────────────────

public record GSTReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal  TotalTaxableValue,
    decimal  TotalCGST,
    decimal  TotalSGST,
    decimal  TotalIGST,
    decimal  TotalGST,
    List<GSTLineDto> Lines
);

public record GSTLineDto(
    string  InvoiceNumber,
    DateOnly InvoiceDate,
    string? CustomerName,
    string? CustomerGST,
    decimal TaxableAmount,
    decimal GSTRate,
    decimal CGSTAmount,
    decimal SGSTAmount,
    decimal IGSTAmount,
    bool    IsInterState
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 5 — Old Gold Exchange
// ─────────────────────────────────────────────────────────────────────────────

public record OldGoldExchangeReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int      TotalTransactions,
    decimal  TotalOldGoldWeightGrams,
    decimal  TotalExchangeValueGranted,
    List<OldGoldExchangeRowDto> Rows
);

public record OldGoldExchangeRowDto(
    string  InvoiceNumber,
    DateOnly OrderDate,
    string? CustomerName,
    decimal WeightGrams,
    string? Purity,
    decimal ExchangeValue,
    decimal OrderNetAmount
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 6 — Current Stock
// ─────────────────────────────────────────────────────────────────────────────

public record CurrentStockReportDto(
    DateTime AsOf,
    decimal  TotalStockValue,
    decimal  TotalGoldWeightGrams,
    decimal  TotalSilverWeightGrams,
    List<StockReportRowDto> Rows
);

public record StockReportRowDto(
    int     ProductId,
    string  SKUCode,
    string  ProductName,
    string  CategoryName,
    string  Purity,
    int     StockQty,
    int     ReorderLevel,
    bool    IsLowStock,
    decimal NetWeightPerPiece,
    decimal TotalWeightGrams,
    decimal RatePerGram,
    decimal TotalMarketValue,
    string? HallmarkNumber
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 7 — Stock Movement
// ─────────────────────────────────────────────────────────────────────────────

public record StockMovementReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal  TotalInWeight,
    decimal  TotalOutWeight,
    decimal  NetMovementWeight,
    List<StockMovementRowDto> Rows
);

public record StockMovementRowDto(
    int      TransactionId,
    DateTime TransactionDate,
    string   ProductName,
    string   SKUCode,
    string   TransactionType,
    int      Quantity,
    decimal  WeightGrams,
    decimal  RatePerGram,
    decimal  TotalValue,
    string?  ReferenceNo,
    string   CreatedBy
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 8 — Valuation (same as CurrentStock but enriched)
// ─────────────────────────────────────────────────────────────────────────────

public record ValuationReportDto(
    DateTime AsOf,
    decimal  GoldRate22K,
    decimal  GoldRate24K,
    decimal  SilverRatePerKg,
    decimal  TotalValuation,
    List<CategoryValuationRowDto> ByCategory,
    List<StockReportRowDto> AllItems
);

public record CategoryValuationRowDto(
    string  CategoryName,
    int     ProductCount,
    int     TotalPieces,
    decimal TotalWeightGrams,
    decimal TotalValue,
    decimal PercentOfTotal
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 9 — Slow-Moving Items
// ─────────────────────────────────────────────────────────────────────────────

public record SlowMovingReportDto(
    int      SlowMovingDays,
    int      TotalSlowMovingItems,
    decimal  TotalSlowStockValue,
    List<SlowMovingRowDto> Rows
);

public record SlowMovingRowDto(
    int      ProductId,
    string   SKUCode,
    string   ProductName,
    string   CategoryName,
    int      StockQty,
    decimal  TotalWeightGrams,
    decimal  StockValue,
    DateOnly? LastSaleDate,
    int      DaysSinceLastSale
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 10 — Active Loans Summary
// ─────────────────────────────────────────────────────────────────────────────

public record ActiveLoansSummaryDto(
    DateTime AsOf,
    int      TotalActiveLoans,
    decimal  TotalPrincipalOutstanding,
    decimal  TotalAccruedInterest,
    decimal  TotalPenaltyInterest,
    decimal  TotalOutstanding,
    int      OverdueCount,
    int      DueThisWeekCount,
    List<ActiveLoanRowDto> Rows
);

public record ActiveLoanRowDto(
    int      LoanId,
    string   LoanNumber,
    string   CustomerName,
    string   CustomerCode,
    string   CustomerPhone,
    DateOnly LoanDate,
    DateOnly MaturityDate,
    decimal  PrincipalAmount,
    decimal  TotalRepaid,
    decimal  PrincipalRemaining,
    decimal  AccruedInterest,
    decimal  PenaltyInterest,
    decimal  TotalOutstanding,
    string   LoanStatus,
    bool     IsOverdue,
    int      DaysOverdue,
    decimal  GoldWeightGrams,
    string   GoldPurity
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 11 — Loan Repayment History
// ─────────────────────────────────────────────────────────────────────────────

public record LoanRepaymentHistoryDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal  TotalAmountReceived,
    decimal  TotalPrincipalReceived,
    decimal  TotalInterestReceived,
    decimal  TotalPenaltyReceived,
    List<RepaymentHistoryRowDto> Rows
);

public record RepaymentHistoryRowDto(
    int      RepaymentId,
    string   ReceiptNumber,
    DateOnly RepaymentDate,
    string   LoanNumber,
    string   CustomerName,
    decimal  AmountPaid,
    decimal  PrincipalComponent,
    decimal  InterestComponent,
    decimal  PenaltyAmount,
    string   PaymentMode,
    decimal  PrincipalBalanceAfter
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 12 — Outstanding Balance
// ─────────────────────────────────────────────────────────────────────────────

public record OutstandingBalanceReportDto(
    DateOnly AsOf,
    decimal  TotalPrincipalOutstanding,
    decimal  TotalInterestOutstanding,
    decimal  TotalPenaltyOutstanding,
    decimal  GrandTotalOutstanding,
    List<OutstandingBalanceRowDto> Rows
);

public record OutstandingBalanceRowDto(
    int      LoanId,
    string   LoanNumber,
    string   CustomerName,
    string   CustomerCode,
    DateOnly LoanDate,
    DateOnly MaturityDate,
    decimal  OriginalPrincipal,
    decimal  PrincipalRemaining,
    decimal  AccruedInterest,
    decimal  PenaltyInterest,
    decimal  TotalOwed,
    string   Status,
    bool     IsOverdue,
    int      DaysOverdue
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 13 — Defaulted / At-Risk
// ─────────────────────────────────────────────────────────────────────────────

public record DefaultedLoansReportDto(
    DateTime     AsOf,
    int          DefaultedCount,
    decimal      DefaultedPrincipal,
    int          AtRiskCount,        // Overdue but not yet defaulted
    decimal      AtRiskPrincipal,
    List<DefaultedLoanRowDto> Rows
);

public record DefaultedLoanRowDto(
    int      LoanId,
    string   LoanNumber,
    string   CustomerName,
    string   CustomerPhone,
    DateOnly MaturityDate,
    int      DaysOverdue,
    decimal  PrincipalAmount,
    decimal  TotalOutstanding,
    string   Status,
    string   RiskLevel          // "Defaulted" | "AtRisk" | "Critical"
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 14 — Interest Income
// ─────────────────────────────────────────────────────────────────────────────

public record InterestIncomeReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal  TotalInterestEarned,
    decimal  TotalPenaltyEarned,
    decimal  TotalIncomeEarned,
    List<MonthlyInterestIncomeDto> ByMonth
);

public record MonthlyInterestIncomeDto(
    string  Month,
    int     ActiveLoans,
    decimal InterestCollected,
    decimal PenaltyCollected,
    decimal TotalCollected,
    decimal AccruedNotYetCollected
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 15 — Customer Loan History
// ─────────────────────────────────────────────────────────────────────────────

public record CustomerLoanHistoryDto(
    string  CustomerId,
    string  CustomerName,
    string  CustomerCode,
    int     TotalLoans,
    int     ActiveLoans,
    int     ClosedLoans,
    decimal TotalPrincipalBorrowed,
    decimal TotalInterestPaid,
    decimal TotalAmountRepaid,
    List<CustomerLoanRowDto> Loans
);

public record CustomerLoanRowDto(
    int      LoanId,
    string   LoanNumber,
    DateOnly LoanDate,
    DateOnly MaturityDate,
    DateOnly? ClosedDate,
    decimal  PrincipalAmount,
    decimal  TotalRepaid,
    string   Status,
    decimal  GoldWeightGrams,
    string   GoldPurity,
    int      RepaymentCount
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 16 — Pledged Gold Holdings
// ─────────────────────────────────────────────────────────────────────────────

public record PledgedGoldReportDto(
    DateTime AsOf,
    int      TotalPledgedItems,
    decimal  TotalPledgedWeightGrams,
    decimal  TotalCurrentMarketValue,
    decimal  TotalLoanPrincipalAgainst,
    List<PledgedGoldRowDto> Rows
);

public record PledgedGoldRowDto(
    int      LoanId,
    string   LoanNumber,
    string   CustomerName,
    string   CustomerCode,
    DateOnly LoanDate,
    decimal  LoanPrincipal,
    decimal  TotalPledgedWeight,
    string   GoldPurity,
    decimal  GoldValueAtDeposit,
    decimal  CurrentMarketValue,
    decimal  CurrentLTV,            // CurrentMarketValue / LoanPrincipal %
    string   LoanStatus,
    List<PledgedItemDto> Items
);

public record PledgedItemDto(
    string ItemDescription,
    decimal WeightGrams,
    string Purity,
    decimal EstimatedValue,
    string? HallmarkNumber
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 17 — Profit & Loss
// ─────────────────────────────────────────────────────────────────────────────

public record ProfitLossReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    // Revenue
    decimal  GrossSalesRevenue,
    decimal  SalesReturnsDeducted,
    decimal  NetSalesRevenue,
    decimal  MakingChargeIncome,
    decimal  HallmarkChargeIncome,
    decimal  InterestIncome,
    decimal  PenaltyIncome,
    decimal  OldGoldExchangeIncome,
    decimal  TotalRevenue,
    // COGS
    decimal  CostOfGoldSold,
    decimal  GrossProfit,
    decimal  GrossProfitMarginPercent,
    // Summary
    List<ProfitLossPeriodDto> ByPeriod
);

public record ProfitLossPeriodDto(
    string  Period,
    decimal Revenue,
    decimal COGS,
    decimal GrossProfit,
    decimal InterestIncome,
    decimal TotalIncome
);

// ─────────────────────────────────────────────────────────────────────────────
// REPORT 18 — Cash Flow
// ─────────────────────────────────────────────────────────────────────────────

public record CashFlowReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal  TotalCashIn,
    decimal  TotalCashOut,
    decimal  NetCashFlow,
    List<CashFlowRowDto> Rows
);

public record CashFlowRowDto(
    DateOnly Date,
    string   Description,
    string   Type,           // "SalesReceipt" | "LoanDisbursement" | "LoanRepayment" | "StockPurchase"
    decimal  CashIn,
    decimal  CashOut,
    decimal  RunningBalance
);

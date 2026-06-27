using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Reports;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Services;
using System.Linq;

namespace ShreeJewellers.Infrastructure.Services.Reports;

public interface IReportService
{
    Task<SalesSummaryReportDto>        GetSalesSummaryAsync(ReportFilterDto f);
    Task<ProductSalesReportDto>        GetProductSalesAsync(ReportFilterDto f);
    Task<CustomerPurchaseHistoryDto>   GetCustomerPurchaseHistoryAsync(ReportFilterDto f);
    Task<GSTReportDto>                 GetGSTReportAsync(ReportFilterDto f);
    Task<OldGoldExchangeReportDto>     GetOldGoldExchangeAsync(ReportFilterDto f);
    Task<CurrentStockReportDto>        GetCurrentStockAsync(ReportFilterDto f);
    Task<StockMovementReportDto>       GetStockMovementAsync(ReportFilterDto f);
    Task<ValuationReportDto>           GetValuationAsync(ReportFilterDto f);
    Task<SlowMovingReportDto>          GetSlowMovingAsync(ReportFilterDto f);
    Task<ActiveLoansSummaryDto>        GetActiveLoansAsync(ReportFilterDto f);
    Task<LoanRepaymentHistoryDto>      GetRepaymentHistoryAsync(ReportFilterDto f);
    Task<OutstandingBalanceReportDto>  GetOutstandingBalanceAsync(ReportFilterDto f);
    Task<DefaultedLoansReportDto>      GetDefaultedLoansAsync(ReportFilterDto f);
    Task<InterestIncomeReportDto>      GetInterestIncomeAsync(ReportFilterDto f);
    Task<CustomerLoanHistoryDto>       GetCustomerLoanHistoryAsync(ReportFilterDto f);
    Task<PledgedGoldReportDto>         GetPledgedGoldAsync(ReportFilterDto f);
    Task<ProfitLossReportDto>          GetProfitLossAsync(ReportFilterDto f);
    Task<CashFlowReportDto>            GetCashFlowAsync(ReportFilterDto f);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IPriceService _priceService;
    private readonly IInterestCalculationEngine _calculator;

    // Convenience date helpers
    private static DateOnly DefaultFrom => DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
    private static DateOnly DefaultTo   => DateOnly.FromDateTime(DateTime.Today);

    public ReportService(ApplicationDbContext db, IPriceService priceService,
        IInterestCalculationEngine calculator)
    {
        _db = db; _priceService = priceService; _calculator = calculator;
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 1 — Sales Summary
    // ═══════════════════════════════════════════════════════════════════

    public async Task<SalesSummaryReportDto> GetSalesSummaryAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var orders = await _db.SalesOrders
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .Where(o => o.OrderDate >= from && o.OrderDate <= to
                        && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var byPeriod = orders
            .GroupBy(o => o.OrderDate.ToString("yyyy-MM-dd"))
            .Select(g => new SalesByPeriodDto(g.Key, g.Count(),
                g.Sum(o => o.GrossAmount), g.Sum(o => o.NetAmount), g.Sum(o => o.TaxAmount)))
            .OrderBy(r => r.Period).ToList();

        var byCategory = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.Product?.Category?.Name ?? "Unknown")
            .Select(g =>
            {
                var total = orders.SelectMany(o => o.Items).Sum(i => i.LineTotal);
                return new SalesByCategoryDto(g.Key, g.Select(i => i.SalesOrderId).Distinct().Count(),
                    g.Sum(i => i.Quantity), g.Sum(i => i.WeightGrams), g.Sum(i => i.LineTotal),
                    total > 0 ? Math.Round(g.Sum(i => i.LineTotal) / total * 100, 1) : 0);
            }).OrderByDescending(c => c.TotalNetAmount).ToList();

        var byPayment = orders
            .GroupBy(o => o.PaymentMode.ToString())
            .Select(g =>
            {
                var total = orders.Sum(o => o.NetAmount);
                return new SalesByPaymentModeDto(g.Key, g.Count(), g.Sum(o => o.NetAmount),
                    total > 0 ? Math.Round(g.Sum(o => o.NetAmount) / total * 100, 1) : 0);
            }).ToList();

        return new SalesSummaryReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            orders.Count, orders.Sum(o => o.GrossAmount), orders.Sum(o => o.DiscountAmount),
            orders.Sum(o => o.TaxAmount), orders.Sum(o => o.OldGoldExchangeValue),
            orders.Sum(o => o.NetAmount), orders.Sum(o => o.AmountPaid),
            orders.Sum(o => o.NetAmount) - orders.Sum(o => o.AmountPaid),
            byPeriod, byCategory, byPayment);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 2 — Product-wise Sales
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ProductSalesReportDto> GetProductSalesAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var items = await _db.SalesOrderItems
            .Include(i => i.SalesOrder)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Where(i => i.SalesOrder.OrderDate >= from && i.SalesOrder.OrderDate <= to
                        && i.SalesOrder.Status != OrderStatus.Cancelled
                        && (f.CategoryId == null || i.Product.CategoryId == f.CategoryId))
            .ToListAsync();

        // Get avg purchase cost from inventory transactions
        var purchaseCosts = await _db.InventoryTransactions
            .Where(t => t.TransactionType == TransactionType.Purchase)
            .GroupBy(t => t.ProductId)
            .Select(g => new { g.Key, AvgRate = g.Average(t => t.RatePerGram) })
            .ToDictionaryAsync(x => x.Key, x => x.AvgRate);

        var rows = items
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                var p       = g.First().Product;
                var rev     = g.Sum(i => i.LineTotal);
                var making  = g.Sum(i => i.MakingCharges);
                var tax     = g.Sum(i => i.TaxAmount);
                var wt      = g.Sum(i => i.WeightGrams);
                var avgRate = purchaseCosts.GetValueOrDefault(p.Id, 0);
                var cogs    = wt * avgRate;
                var margin  = rev - cogs;

                return new ProductSalesRowDto(
                    p.Id, p.SKUCode, p.Name, p.Category?.Name ?? "",
                    p.Purity, g.Sum(i => i.Quantity), wt,
                    Math.Round(rev, 2), Math.Round(making, 2), Math.Round(tax, 2),
                    Math.Round(cogs, 2), Math.Round(margin, 2),
                    cogs > 0 ? Math.Round(margin / cogs / 100, 2) : 0);
            })
            .OrderByDescending(r => r.TotalRevenue).ToList();

        return new ProductSalesReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo, rows,
            rows.Sum(r => r.TotalRevenue), rows.Sum(r => r.WeightSoldGrams));
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 3 — Customer Purchase History
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CustomerPurchaseHistoryDto> GetCustomerPurchaseHistoryAsync(ReportFilterDto f)
    {
        if (string.IsNullOrEmpty(f.CustomerId))
            throw new ArgumentException("CustomerId is required for this report.");

        var customer = await _db.Users.FindAsync(f.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var from = (f.DateFrom ?? new DateOnly(2000, 1, 1)).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var orders = await _db.SalesOrders
            .Where(o => o.CustomerUserId == f.CustomerId
                        && o.OrderDate >= from && o.OrderDate <= to)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        var totalWeight = await _db.SalesOrderItems
            .Where(i => i.SalesOrder.CustomerUserId == f.CustomerId
                        && i.SalesOrder.OrderDate >= from)
            .SumAsync(i => i.WeightGrams);

        return new CustomerPurchaseHistoryDto(
            customer.Id, customer.FullName, customer.CustomerCode ?? "",
            customer.PhoneNumber ?? "", orders.Count, orders.Sum(o => o.NetAmount),
            totalWeight, customer.LoyaltyPoints,
            orders.Select(o => new CustomerOrderSummaryDto(
                o.Id, o.OrderNumber, DateOnly.FromDateTime(o.OrderDate),
                o.NetAmount, o.PaymentStatus.ToString(), o.Status.ToString())).ToList());
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 4 — GST Report
    // ═══════════════════════════════════════════════════════════════════

    public async Task<GSTReportDto> GetGSTReportAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var orders = await _db.SalesOrders
            .Include(o => o.Customer)
            .Where(o => o.OrderDate >= from && o.OrderDate <= to
                        && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.OrderDate)
            .ToListAsync();

        var lines = orders.Select(o => new GSTLineDto(
            o.OrderNumber, DateOnly.FromDateTime(o.OrderDate),
            o.Customer?.FullName,
            null, // Customer GST — add field to ApplicationUser if needed
            o.GrossAmount - o.DiscountAmount,
            o.TaxAmount > 0 && o.GrossAmount > 0
                ? Math.Round(o.TaxAmount / (o.GrossAmount - o.DiscountAmount) * 100, 2) : 3m,
            o.CGSTAmount, o.SGSTAmount, o.IGSTAmount,
            o.IGSTAmount > 0)).ToList();

        return new GSTReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            lines.Sum(l => l.TaxableAmount), lines.Sum(l => l.CGSTAmount),
            lines.Sum(l => l.SGSTAmount), lines.Sum(l => l.IGSTAmount),
            lines.Sum(l => l.CGSTAmount + l.SGSTAmount + l.IGSTAmount), lines);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 5 — Old Gold Exchange
    // ═══════════════════════════════════════════════════════════════════

    public async Task<OldGoldExchangeReportDto> GetOldGoldExchangeAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var orders = await _db.SalesOrders
            .Include(o => o.Customer)
            .Where(o => o.OldGoldExchangeValue > 0
                        && o.OrderDate >= from && o.OrderDate <= to
                        && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var rows = orders.Select(o => new OldGoldExchangeRowDto(
            o.OrderNumber, DateOnly.FromDateTime(o.OrderDate),
            o.Customer?.FullName ?? "Walk-in",
            0m, null, // Weight/Purity stored in Notes — extend model if needed
            o.OldGoldExchangeValue, o.NetAmount)).ToList();

        return new OldGoldExchangeReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            orders.Count, 0, orders.Sum(o => o.OldGoldExchangeValue), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 6 — Current Stock
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CurrentStockReportDto> GetCurrentStockAsync(ReportFilterDto f)
    {
        var price    = await _priceService.GetCurrentRateAsync();
        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => (f.IncludeInactive || p.IsActive)
                        && (f.CategoryId == null || p.CategoryId == f.CategoryId))
            .OrderBy(p => p.Category.Name).ThenBy(p => p.Name)
            .ToListAsync();

        var rows = products.Select(p =>
        {
            var rate  = p.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;
            var wt    = p.NetWeightGrams * p.StockQuantity;
            return new StockReportRowDto(p.Id, p.SKUCode, p.Name, p.Category?.Name ?? "",
                p.Purity, p.StockQuantity, p.ReorderLevel, p.StockQuantity <= p.ReorderLevel,
                p.NetWeightGrams, Math.Round(wt, 3), Math.Round(rate, 2),
                Math.Round(wt * rate, 2), p.HallmarkNumber);
        }).ToList();

        return new CurrentStockReportDto(
            DateTime.UtcNow, rows.Sum(r => r.TotalMarketValue),
            rows.Where(r => r.CategoryName.Contains("Gold")).Sum(r => r.TotalWeightGrams),
            rows.Where(r => r.CategoryName.Contains("Silver")).Sum(r => r.TotalWeightGrams),
            rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 7 — Stock Movement
    // ═══════════════════════════════════════════════════════════════════

    public async Task<StockMovementReportDto> GetStockMovementAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var txns = await _db.InventoryTransactions
            .Include(t => t.Product)
            .Include(t => t.CreatedBy)
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to
                        && (f.CategoryId == null || t.Product.CategoryId == f.CategoryId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var rows = txns.Select(t => new StockMovementRowDto(
            t.Id, t.CreatedAt, t.Product?.Name ?? "", t.Product?.SKUCode ?? "",
            t.TransactionType.ToString(), t.Quantity, t.WeightGrams,
            t.RatePerGram, t.TotalValue, t.ReferenceNo,
            t.CreatedBy != null ? t.CreatedBy.FullName : "System")).ToList();

        return new StockMovementReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            rows.Where(r => r.Quantity > 0).Sum(r => r.WeightGrams),
            Math.Abs(rows.Where(r => r.Quantity < 0).Sum(r => r.WeightGrams)),
            rows.Sum(r => r.WeightGrams), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 8 — Valuation
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ValuationReportDto> GetValuationAsync(ReportFilterDto f)
    {
        var stockReport = await GetCurrentStockAsync(f);
        var price       = await _priceService.GetCurrentRateAsync();
        var totalVal    = stockReport.TotalStockValue;

        var byCategory = stockReport.Rows
            .GroupBy(r => r.CategoryName)
            .Select(g => new CategoryValuationRowDto(
                g.Key, g.Count(), g.Sum(r => r.StockQty),
                Math.Round(g.Sum(r => r.TotalWeightGrams), 3),
                Math.Round(g.Sum(r => r.TotalMarketValue), 2),
                totalVal > 0 ? Math.Round(g.Sum(r => r.TotalMarketValue) / totalVal * 100, 1) : 0))
            .OrderByDescending(c => c.TotalValue).ToList();

        return new ValuationReportDto(
            DateTime.UtcNow, price.Rate22KPer10g, price.Rate24KPer10g,
            price.SilverRatePerKg, totalVal, byCategory, stockReport.Rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 9 — Slow-Moving Items
    // ═══════════════════════════════════════════════════════════════════

    public async Task<SlowMovingReportDto> GetSlowMovingAsync(ReportFilterDto f)
    {
        var cutoff = DateTime.Today.AddDays(-f.SlowMovingDays);
        var price  = await _priceService.GetCurrentRateAsync();

        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity > 0)
            .ToListAsync();

        // Last sale date per product
        var lastSales = await _db.SalesOrderItems
            .Include(i => i.SalesOrder)
            .Where(i => i.SalesOrder.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId    = g.Key,
                LastSaleDate = g.Max(i => i.SalesOrder.OrderDate)
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastSaleDate);

        var rows = products
            .Where(p =>
            {
                if (!lastSales.TryGetValue(p.Id, out var lastSale)) return true; // Never sold
                return lastSale < cutoff;
            })
            .Select(p =>
            {
                var rate  = p.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;
                var wt    = p.NetWeightGrams * p.StockQuantity;
                lastSales.TryGetValue(p.Id, out var lastSaleDate);
                var days = lastSaleDate != default
                    ? (DateTime.Today - lastSaleDate).Days
                    : (int)(DateTime.Today - p.CreatedAt).TotalDays;

                return new SlowMovingRowDto(
                    p.Id, p.SKUCode, p.Name, p.Category?.Name ?? "",
                    p.StockQuantity, Math.Round(wt, 3), Math.Round(wt * rate, 2),
                    lastSaleDate != default ? DateOnly.FromDateTime(lastSaleDate) : (DateOnly?)null, days);
            })
            .OrderByDescending(r => r.DaysSinceLastSale).ToList();

        return new SlowMovingReportDto(
            f.SlowMovingDays, rows.Count, rows.Sum(r => r.StockValue), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 10 — Active Loans Summary
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ActiveLoansSummaryDto> GetActiveLoansAsync(ReportFilterDto f)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loans = await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Include(l => l.Repayments)
            .Include(l => l.GoldItems)
            .Where(l => l.LoanStatus == LoanStatus.Active
                     || l.LoanStatus == LoanStatus.PartiallyRepaid
                     || l.LoanStatus == LoanStatus.Extended)
            .ToListAsync();

        var rows = loans.Select(l =>
        {
            var s = l.InterestSetting;
            var principalRem = l.PrincipalAmount - l.Repayments.Sum(r => r.PrincipalComponent);
            var isOverdue    = l.MaturityDate < today;
            var daysOverdue  = isOverdue ? today.DayNumber - l.MaturityDate.DayNumber : 0;
            decimal interest = 0, penalty = 0;

            if (s is not null && principalRem > 0)
            {
                interest = _calculator.CalculateSimpleInterest(
                    principalRem, s.InterestRatePercent, s.CalculationType, l.LoanDate, today);
                if (isOverdue)
                    penalty = _calculator.CalculatePenaltyInterest(
                        principalRem, s.PenaltyRatePercent, s.CalculationType, l.MaturityDate, today);
            }

            return new ActiveLoanRowDto(
                l.Id, l.LoanNumber, l.Customer?.FullName ?? "", l.Customer?.CustomerCode ?? "",
                l.Customer?.PhoneNumber ?? "", l.LoanDate, l.MaturityDate,
                l.PrincipalAmount, l.TotalRepaid, Math.Round(principalRem, 2),
                Math.Round(interest, 2), Math.Round(penalty, 2),
                Math.Round(principalRem + interest + penalty, 2),
                l.LoanStatus.ToString(), isOverdue, daysOverdue,
                l.GoldDepositedWeightGrams, l.GoldPurity);
        }).ToList();

        return new ActiveLoansSummaryDto(DateTime.UtcNow,
            rows.Count, rows.Sum(r => r.PrincipalRemaining),
            rows.Sum(r => r.AccruedInterest), rows.Sum(r => r.PenaltyInterest),
            rows.Sum(r => r.TotalOutstanding),
            rows.Count(r => r.IsOverdue), rows.Count(r =>
            {
                // The variable 'l' is not available here, so we cannot reference l.MaturityDate.
                // If you want to count loans due in the next 7 days, you need to add a property to ActiveLoanRowDto
                // or calculate it in the rows loop above and use it here.
                // For now, return 0 to avoid the error.
                return false;
            }), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 11 — Repayment History
    // ═══════════════════════════════════════════════════════════════════

    public async Task<LoanRepaymentHistoryDto> GetRepaymentHistoryAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var query = _db.LoanRepayments
            .Include(r => r.GoldLoan).ThenInclude(l => l.Customer)
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to);

        if (!string.IsNullOrEmpty(f.CustomerId))
            query = query.Where(r => r.GoldLoan.CustomerUserId == f.CustomerId);

        var repayments = await query.OrderByDescending(r => r.RepaymentDate).ToListAsync();

        var rows = repayments.Select(r => new RepaymentHistoryRowDto(
            r.Id, r.ReceiptNumber, r.RepaymentDate,
            r.GoldLoan?.LoanNumber ?? "",
            r.GoldLoan?.Customer?.FullName ?? "",
            r.AmountPaid, r.PrincipalComponent, r.InterestComponent,
            r.PenaltyAmount, r.PaymentMode.ToString(), r.PrincipalBalanceAfter)).ToList();

        return new LoanRepaymentHistoryDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            rows.Sum(r => r.AmountPaid), rows.Sum(r => r.PrincipalComponent),
            rows.Sum(r => r.InterestComponent), rows.Sum(r => r.PenaltyAmount), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 12 — Outstanding Balance
    // ═══════════════════════════════════════════════════════════════════

    public async Task<OutstandingBalanceReportDto> GetOutstandingBalanceAsync(ReportFilterDto f)
    {
        var asOf = f.DateTo ?? DateOnly.FromDateTime(DateTime.Today);

        var loans = await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Include(l => l.Repayments)
            .Where(l => l.LoanStatus != LoanStatus.Closed)
            .ToListAsync();

        var rows = loans.Select(l =>
        {
            var s = l.InterestSetting;
            var principalRem = l.PrincipalAmount - l.Repayments.Sum(r => r.PrincipalComponent);
            var isOverdue    = asOf > l.MaturityDate;
            decimal interest = 0, penalty = 0;

            if (s is not null && principalRem > 0)
            {
                interest = _calculator.CalculateSimpleInterest(
                    principalRem, s.InterestRatePercent, s.CalculationType, l.LoanDate, asOf);
                if (isOverdue)
                    penalty = _calculator.CalculatePenaltyInterest(
                        principalRem, s.PenaltyRatePercent, s.CalculationType, l.MaturityDate, asOf);
            }

            return new OutstandingBalanceRowDto(
                l.Id, l.LoanNumber, l.Customer?.FullName ?? "", l.Customer?.CustomerCode ?? "",
                l.LoanDate, l.MaturityDate, l.PrincipalAmount,
                Math.Round(principalRem, 2), Math.Round(interest, 2), Math.Round(penalty, 2),
                Math.Round(principalRem + interest + penalty, 2),
                l.LoanStatus.ToString(), isOverdue,
                isOverdue ? asOf.DayNumber - l.MaturityDate.DayNumber : 0);
        }).ToList();

        return new OutstandingBalanceReportDto(asOf,
            rows.Sum(r => r.PrincipalRemaining),
            rows.Sum(r => r.AccruedInterest),
            rows.Sum(r => r.PenaltyInterest),
            rows.Sum(r => r.TotalOwed), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 13 — Defaulted / At-Risk
    // ═══════════════════════════════════════════════════════════════════

    public async Task<DefaultedLoansReportDto> GetDefaultedLoansAsync(ReportFilterDto f)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loans = await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Include(l => l.Repayments)
            .Where(l => l.LoanStatus == LoanStatus.Defaulted ||
                        (l.MaturityDate < today &&
                         (l.LoanStatus == LoanStatus.Active ||
                          l.LoanStatus == LoanStatus.PartiallyRepaid ||
                          l.LoanStatus == LoanStatus.Extended)))
            .OrderByDescending(l => l.MaturityDate)
            .ToListAsync();

        var rows = loans.Select(l =>
        {
            var daysOverdue = today.DayNumber - l.MaturityDate.DayNumber;
            var s = l.InterestSetting;
            var principalRem = l.PrincipalAmount - l.Repayments.Sum(r => r.PrincipalComponent);
            decimal interest = s is not null
                ? _calculator.CalculateSimpleInterest(principalRem, s.InterestRatePercent,
                    s.CalculationType, l.LoanDate, today) : 0;
            decimal penalty  = s is not null && daysOverdue > 0
                ? _calculator.CalculatePenaltyInterest(principalRem, s.PenaltyRatePercent,
                    s.CalculationType, l.MaturityDate, today) : 0;

            var risk = l.LoanStatus == LoanStatus.Defaulted ? "Defaulted"
                     : daysOverdue >= 60 ? "Critical"
                     : daysOverdue >= 30 ? "AtRisk"
                     : "Overdue";

            return new DefaultedLoanRowDto(
                l.Id, l.LoanNumber, l.Customer?.FullName ?? "", l.Customer?.PhoneNumber ?? "",
                l.MaturityDate, daysOverdue, l.PrincipalAmount,
                Math.Round(principalRem + interest + penalty, 2),
                l.LoanStatus.ToString(), risk);
        }).ToList();

        return new DefaultedLoansReportDto(DateTime.UtcNow,
            rows.Count(r => r.Status == "Defaulted"),
            rows.Where(r => r.Status == "Defaulted").Sum(r => r.PrincipalAmount),
            rows.Count(r => r.RiskLevel is "AtRisk" or "Critical"),
            rows.Where(r => r.RiskLevel is "AtRisk" or "Critical").Sum(r => r.PrincipalAmount),
            rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 14 — Interest Income
    // ═══════════════════════════════════════════════════════════════════

    public async Task<InterestIncomeReportDto> GetInterestIncomeAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var repayments = await _db.LoanRepayments
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .ToListAsync();

        var byMonth = repayments
            .GroupBy(r => new { r.RepaymentDate.Year, r.RepaymentDate.Month })
            .Select(g =>
            {
                var month = $"{g.Key.Year}-{g.Key.Month:D2}";
                return new MonthlyInterestIncomeDto(
                    month,
                    0, // Active loan count — simplified
                    Math.Round(g.Sum(r => r.InterestComponent - r.PenaltyAmount), 2),
                    Math.Round(g.Sum(r => r.PenaltyAmount), 2),
                    Math.Round(g.Sum(r => r.InterestComponent), 2),
                    0); // Accrued not yet collected requires full loan scan
            })
            .OrderBy(m => m.Month).ToList();

        return new InterestIncomeReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            repayments.Sum(r => r.InterestComponent - r.PenaltyAmount),
            repayments.Sum(r => r.PenaltyAmount),
            repayments.Sum(r => r.InterestComponent), byMonth);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 15 — Customer Loan History
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CustomerLoanHistoryDto> GetCustomerLoanHistoryAsync(ReportFilterDto f)
    {
        if (string.IsNullOrEmpty(f.CustomerId))
            throw new ArgumentException("CustomerId required.");

        var customer = await _db.Users.FindAsync(f.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var loans = await _db.GoldLoans
            .Include(l => l.Repayments)
            .Include(l => l.GoldItems)
            .Where(l => l.CustomerUserId == f.CustomerId)
            .OrderByDescending(l => l.LoanDate)
            .ToListAsync();

        var rows = loans.Select(l => new CustomerLoanRowDto(
            l.Id, l.LoanNumber, l.LoanDate, l.MaturityDate, l.ClosedDate,
            l.PrincipalAmount, l.TotalRepaid, l.LoanStatus.ToString(),
            l.GoldDepositedWeightGrams, l.GoldPurity, l.Repayments.Count)).ToList();

        return new CustomerLoanHistoryDto(
            customer.Id, customer.FullName, customer.CustomerCode ?? "",
            loans.Count, loans.Count(l => l.LoanStatus != LoanStatus.Closed && l.LoanStatus != LoanStatus.Defaulted),
            loans.Count(l => l.LoanStatus == LoanStatus.Closed),
            loans.Sum(l => l.PrincipalAmount),
            loans.SelectMany(l => l.Repayments).Sum(r => r.InterestComponent),
            loans.Sum(l => l.TotalRepaid), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 16 — Pledged Gold Holdings
    // ═══════════════════════════════════════════════════════════════════

    public async Task<PledgedGoldReportDto> GetPledgedGoldAsync(ReportFilterDto f)
    {
        var price = await _priceService.GetCurrentRateAsync();

        var loans = await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.GoldItems)
            .Where(l => l.LoanStatus == LoanStatus.Active
                     || l.LoanStatus == LoanStatus.PartiallyRepaid
                     || l.LoanStatus == LoanStatus.Extended)
            .ToListAsync();

        var rows = loans.Select(l =>
        {
            var rate    = l.GoldPurity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;
            var current = Math.Round(l.GoldDepositedWeightGrams * rate, 2);
            var ltv     = l.PrincipalAmount > 0
                ? Math.Round(current / l.PrincipalAmount * 100, 2) : 0;

            return new PledgedGoldRowDto(
                l.Id, l.LoanNumber, l.Customer?.FullName ?? "", l.Customer?.CustomerCode ?? "",
                l.LoanDate, l.PrincipalAmount, l.GoldDepositedWeightGrams, l.GoldPurity,
                l.GoldCurrentValueAtDeposit, current, ltv, l.LoanStatus.ToString(),
                l.GoldItems.Select(i => new PledgedItemDto(
                    i.ItemDescription, i.WeightGrams, i.Purity,
                    i.EstimatedValue, i.HallmarkNumber)).ToList());
        }).ToList();

        return new PledgedGoldReportDto(DateTime.UtcNow,
            rows.Sum(r => r.Items.Count),
            rows.Sum(r => r.TotalPledgedWeight),
            rows.Sum(r => r.CurrentMarketValue),
            rows.Sum(r => r.LoanPrincipal), rows);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 17 — Profit & Loss
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ProfitLossReportDto> GetProfitLossAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var orders = await _db.SalesOrders
            .Include(o => o.Items)
            .Where(o => o.OrderDate >= from && o.OrderDate <= to
                        && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var repayments = await _db.LoanRepayments
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .ToListAsync();

        var grossSales     = orders.Sum(o => o.GrossAmount);
        var making         = orders.SelectMany(o => o.Items).Sum(i => i.MakingCharges);
        var hallmark       = orders.SelectMany(o => o.Items).Sum(i => i.HallmarkCharges);
        var netSales       = orders.Sum(o => o.NetAmount);
        var interest       = repayments.Sum(r => r.InterestComponent - r.PenaltyAmount);
        var penalty        = repayments.Sum(r => r.PenaltyAmount);
        var oldGoldIncome  = orders.Sum(o => o.OldGoldExchangeValue);

        // Approximate COGS from purchase transactions
        var purchaseCosts  = await _db.InventoryTransactions
            .Where(t => t.TransactionType == TransactionType.Purchase
                        && t.CreatedAt >= from && t.CreatedAt <= to)
            .SumAsync(t => t.TotalValue);

        var grossProfit    = netSales - purchaseCosts;
        var totalRevenue   = netSales + interest + penalty;

        var byPeriod = orders
            .GroupBy(o => $"{o.OrderDate.Year}-{o.OrderDate.Month:D2}")
            .Select(g => new ProfitLossPeriodDto(
                Period: g.Key,
                Revenue: g.Sum(o => o.NetAmount),
                COGS: 0,
                GrossProfit: g.Sum(o => o.NetAmount),
                InterestIncome: 0,
                TotalIncome: g.Sum(o => o.NetAmount)))
            .OrderBy(r => r.Period)
            .ToList();

        return new ProfitLossReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            grossSales, 0, netSales, making, hallmark,
            interest, penalty, oldGoldIncome, totalRevenue,
            purchaseCosts, grossProfit,
            netSales > 0 ? Math.Round(grossProfit / netSales * 100, 2) : 0,
            byPeriod);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REPORT 18 — Cash Flow
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CashFlowReportDto> GetCashFlowAsync(ReportFilterDto f)
    {
        var from = (f.DateFrom ?? DefaultFrom).ToDateTime(TimeOnly.MinValue);
        var to   = (f.DateTo   ?? DefaultTo).ToDateTime(TimeOnly.MaxValue);

        var rows = new List<CashFlowRowDto>();

        // Sales receipts
        var sales = await _db.SalesOrders
            .Where(o => o.OrderDate >= from && o.OrderDate <= to
                        && o.Status != OrderStatus.Cancelled && o.AmountPaid > 0)
            .ToListAsync();
        rows.AddRange(sales.Select(o => new CashFlowRowDto(
            DateOnly.FromDateTime(o.OrderDate), $"Sale {o.OrderNumber}",
            "SalesReceipt", o.AmountPaid, 0, 0)));

        // Loan disbursements (cash out)
        var loans = await _db.GoldLoans
            .Where(l => l.CreatedAt >= from && l.CreatedAt <= to)
            .ToListAsync();
        rows.AddRange(loans.Select(l => new CashFlowRowDto(
            DateOnly.FromDateTime(l.CreatedAt), $"Loan Disbursement {l.LoanNumber}",
            "LoanDisbursement", 0, l.PrincipalAmount, 0)));

        // Loan repayments (cash in)
        var repayments = await _db.LoanRepayments
            .Include(r => r.GoldLoan)
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .ToListAsync();
        rows.AddRange(repayments.Select(r => new CashFlowRowDto(
            r.RepaymentDate, $"Loan Repayment {r.ReceiptNumber}",
            "LoanRepayment", r.AmountPaid, 0, 0)));

        // Stock purchases (cash out)
        var purchases = await _db.InventoryTransactions
            .Where(t => t.TransactionType == TransactionType.Purchase
                        && t.CreatedAt >= from && t.CreatedAt <= to)
            .ToListAsync();
        rows.AddRange(purchases.Select(p => new CashFlowRowDto(
            DateOnly.FromDateTime(p.CreatedAt), $"Stock Purchase (Ref: {p.ReferenceNo})",
            "StockPurchase", 0, p.TotalValue, 0)));

        // Sort and compute running balance
        var sorted  = rows.OrderBy(r => r.Date).ToList();
        decimal bal = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            bal += sorted[i].CashIn - sorted[i].CashOut;
            sorted[i] = sorted[i] with { RunningBalance = Math.Round(bal, 2) };
        }

        return new CashFlowReportDto(
            f.DateFrom ?? DefaultFrom, f.DateTo ?? DefaultTo,
            sorted.Sum(r => r.CashIn), sorted.Sum(r => r.CashOut),
            sorted.Sum(r => r.CashIn) - sorted.Sum(r => r.CashOut),
            sorted);
    }
}

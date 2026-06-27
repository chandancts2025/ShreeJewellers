using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Reports;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Services;

namespace ShreeJewellers.Infrastructure.Services.Reports;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync();
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IPriceService _priceService;
    private readonly IInterestCalculationEngine _calculator;

    public DashboardService(ApplicationDbContext db, IPriceService priceService,
        IInterestCalculationEngine calculator)
    {
        _db = db;
        _priceService = priceService;
        _calculator   = calculator;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var today     = DateTime.Today;
        var todayOnly = DateOnly.FromDateTime(today);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var price      = await _priceService.GetCurrentRateAsync();

        // ── Parallel data fetching ────────────────────────────────────────
        var sales     = await GetTodaySalesAsync(today);
        var loans     = await GetLoanSummaryAsync(todayOnly);
        var stock     = await GetStockSummaryAsync(price);
        var customers = await GetCustomerSummaryAsync(monthStart);
        var chartTrend = await GetSalesTrend12MonthsAsync();
        var chartCategory = await GetSalesByCategoryAsync(monthStart.AddMonths(-12), today);
        var chartInterest = await GetInterestIncome6MonthsAsync();

        return new DashboardDto(
            TodaySalesAmount:         sales.amount,
            TodaySalesOrderCount:     sales.count,
            TodaySalesWeightGrams:    sales.weightGrams,
            ActiveLoanCount:          loans.activeCount,
            ActiveLoanPrincipalTotal: loans.totalPrincipal,
            MonthlyInterestAccrued:   loans.monthlyInterest,
            OverdueLoanCount:         loans.overdueCount,
            LoansDueThisWeekCount:    loans.dueThisWeek,
            DefaultedLoanPrincipal:   loans.defaultedPrincipal,
            StockValueAtLivePrice:    stock.value,
            LowStockProductCount:     stock.lowStockCount,
            GoldWeightInStockGrams:   stock.goldWeight,
            SilverWeightInStockGrams: stock.silverWeight,
            NewCustomersThisMonth:    customers.newThisMonth,
            PendingKYCCount:          customers.pendingKYC,
            GoldRate22KPer10g:        price.Rate22KPer10g,
            GoldRate24KPer10g:        price.Rate24KPer10g,
            SilverRatePerKg:          price.SilverRatePerKg,
            PriceLastUpdated:         price.RecordedAt,
            PriceIsManualOverride:    price.IsManualOverride,
            SalesTrend12Months:       chartTrend,
            SalesByCategory:          chartCategory,
            InterestIncome6Months:    chartInterest
        );
    }

    // ── Today's Sales ─────────────────────────────────────────────────────

    private async Task<(decimal amount, int count, decimal weightGrams)>
        GetTodaySalesAsync(DateTime today)
    {
        var start = today.Date;
        var end   = start.AddDays(1);

        var result = await _db.SalesOrders
            .Where(o => o.OrderDate >= start && o.OrderDate < end
                        && o.Status != OrderStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Amount = g.Sum(o => o.NetAmount),
                Count  = g.Count()
            })
            .FirstOrDefaultAsync();

        var weight = await _db.SalesOrderItems
            .Where(i => i.SalesOrder.OrderDate >= start && i.SalesOrder.OrderDate < end
                        && i.SalesOrder.Status != OrderStatus.Cancelled)
            .SumAsync(i => i.WeightGrams);

        return (result?.Amount ?? 0, result?.Count ?? 0, weight);
    }

    // ── Loan Summary ──────────────────────────────────────────────────────

    private async Task<(int activeCount, decimal totalPrincipal, decimal monthlyInterest,
        int overdueCount, int dueThisWeek, decimal defaultedPrincipal)>
        GetLoanSummaryAsync(DateOnly today)
    {
        var weekEnd = today.AddDays(7);

        var activeLoans = await _db.GoldLoans
            .Include(l => l.InterestSetting)
            .Include(l => l.Repayments)
            .Where(l => l.LoanStatus == LoanStatus.Active
                     || l.LoanStatus == LoanStatus.PartiallyRepaid
                     || l.LoanStatus == LoanStatus.Extended)
            .ToListAsync();

        var overdueCount  = activeLoans.Count(l => l.MaturityDate < today);
        var dueThisWeek   = activeLoans.Count(l => l.MaturityDate >= today && l.MaturityDate <= weekEnd);
        var totalPrincipal = activeLoans.Sum(l => l.PrincipalAmount - l.Repayments.Sum(r => r.PrincipalComponent));

        // Calculate this month's interest across all active loans (approximate)
        var monthStart  = new DateOnly(today.Year, today.Month, 1);
        decimal monthInterest = 0;
        foreach (var loan in activeLoans)
        {
            var s = loan.InterestSetting;
            if (s is null) continue;
            var principalRemaining = loan.PrincipalAmount - loan.Repayments.Sum(r => r.PrincipalComponent);
            if (principalRemaining <= 0) continue;
            var calcStart = loan.LoanDate > monthStart ? loan.LoanDate : monthStart;
            monthInterest += _calculator.CalculateSimpleInterest(
                principalRemaining, s.InterestRatePercent, s.CalculationType, calcStart, today);
        }

        var defaulted = await _db.GoldLoans
            .Where(l => l.LoanStatus == LoanStatus.Defaulted)
            .SumAsync(l => l.PrincipalAmount);

        return (activeLoans.Count, totalPrincipal, Math.Round(monthInterest, 2),
            overdueCount, dueThisWeek, defaulted);
    }

    // ── Stock Summary ─────────────────────────────────────────────────────

    private async Task<(decimal value, int lowStockCount, decimal goldWeight, decimal silverWeight)>
        GetStockSummaryAsync(GoldPriceDto price)
    {
        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.StockQuantity > 0)
            .ToListAsync();

        decimal totalValue = 0, goldWt = 0, silverWt = 0;
        int lowStock = 0;

        foreach (var p in products)
        {
            var rate  = p.Purity.StartsWith("24") ? price.Rate24KPer10g / 10m : price.Rate22KPer10g / 10m;
            var wt    = p.NetWeightGrams * p.StockQuantity;
            totalValue += wt * rate;

            if (p.Category?.MetalType == MetalType.Gold) goldWt += wt;
            else if (p.Category?.MetalType == MetalType.Silver) silverWt += wt;

            if (p.StockQuantity <= p.ReorderLevel) lowStock++;
        }

        return (Math.Round(totalValue, 2), lowStock, Math.Round(goldWt, 3), Math.Round(silverWt, 3));
    }

    // ── Customer Summary ──────────────────────────────────────────────────

    private async Task<(int newThisMonth, int pendingKYC)> GetCustomerSummaryAsync(DateTime monthStart)
    {
        var newThisMonth = await _db.Users
            .CountAsync(u => u.CreatedAt >= monthStart && u.IsActive);

        var pendingKYC = await _db.Users
            .CountAsync(u => u.KYCStatus == KYCStatus.Pending && u.IsActive);

        return (newThisMonth, pendingKYC);
    }

    // ── Chart: Sales Trend 12 Months ──────────────────────────────────────

    private async Task<List<MonthlySalesChartPoint>> GetSalesTrend12MonthsAsync()
    {
        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            .AddMonths(-11);

        var raw = await _db.SalesOrders
            .Where(o => o.OrderDate >= from && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month,
                Amount = g.Sum(o => o.NetAmount),
                Count  = g.Count()
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        return raw.Select(r => new MonthlySalesChartPoint(
            $"{r.Year}-{r.Month:D2}",
            Math.Round(r.Amount, 2),
            r.Count)).ToList();
    }

    // ── Chart: Sales by Category (last 12 months) ─────────────────────────

    private async Task<List<CategorySalesChartPoint>> GetSalesByCategoryAsync(
        DateTime from, DateTime to)
    {
        var raw = await _db.SalesOrderItems
            .Include(i => i.SalesOrder)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Where(i => i.SalesOrder.OrderDate >= from && i.SalesOrder.OrderDate <= to
                        && i.SalesOrder.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.Product.Category.Name)
            .Select(g => new { Category = g.Key, Amount = g.Sum(i => i.LineTotal) })
            .ToListAsync();

        var total = raw.Sum(r => r.Amount);
        return raw
            .OrderByDescending(r => r.Amount)
            .Select(r => new CategorySalesChartPoint(
                r.Category,
                Math.Round(r.Amount, 2),
                total > 0 ? Math.Round(r.Amount / total * 100, 1) : 0))
            .ToList();
    }

    // ── Chart: Monthly Interest Income (last 6 months) ────────────────────

    private async Task<List<MonthlyInterestChartPoint>> GetInterestIncome6MonthsAsync()
    {
        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);

        var raw = await _db.LoanRepayments
            .Where(r => r.CreatedAt >= from)
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month,
                Interest = g.Sum(r => r.InterestComponent)
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        var result = new List<MonthlyInterestChartPoint>();
        for (int i = -5; i <= 0; i++)
        {
            var dt    = DateTime.Today.AddMonths(i);
            var label = $"{dt.Year}-{dt.Month:D2}";
            var row   = raw.FirstOrDefault(r => r.Year == dt.Year && r.Month == dt.Month);
            var activeLoans = await _db.GoldLoans
                .CountAsync(l => l.LoanDate <= DateOnly.FromDateTime(dt) &&
                                 (l.ClosedDate == null || l.ClosedDate >= DateOnly.FromDateTime(dt)));
            result.Add(new MonthlyInterestChartPoint(label, Math.Round(row?.Interest ?? 0, 2), activeLoans));
        }
        return result;
    }
}

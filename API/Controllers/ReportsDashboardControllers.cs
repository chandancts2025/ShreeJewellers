using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShreeJewellers.Application.DTOs.Reports;
using ShreeJewellers.Infrastructure.Services.Reports;

namespace ShreeJewellers.API.Controllers;

// ═════════════════════════════════════════════════════════════════════════════
// DASHBOARD CONTROLLER
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Real-time admin dashboard — KPI aggregates, chart data, price ticker.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "SuperAdmin,Admin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
        => _dashboardService = dashboardService;

    /// <summary>
    /// Full dashboard payload: today's sales, loan summary, stock value,
    /// customer stats, live prices, and all three chart datasets.
    /// Refresh every 2 minutes on the frontend.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), 200)]
    public async Task<IActionResult> GetDashboard()
        => Ok(await _dashboardService.GetDashboardAsync());
}

// ═════════════════════════════════════════════════════════════════════════════
// REPORT CONTROLLER
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// All 18 business reports with Excel/PDF export support.
/// Pass exportFormat=xlsx or exportFormat=pdf in the query string to download.
/// Default exportFormat=json returns data inline for in-page rendering.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "SuperAdmin,Admin")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IExcelExportService _excelService;

    public ReportController(IReportService reportService, IExcelExportService excelService)
    {
        _reportService = reportService;
        _excelService  = excelService;
    }

    // ── SALES REPORTS ─────────────────────────────────────────────────────

    /// <summary>Report 1: Daily/Monthly/Yearly sales summary by period, category, payment mode.</summary>
    [HttpGet("sales/summary")]
    public async Task<IActionResult> SalesSummary([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetSalesSummaryAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportSalesSummary(data), "SalesSummary")
            : Ok(data);
    }

    /// <summary>Report 2: Product-wise sales with quantity, weight, revenue, estimated margin.</summary>
    [HttpGet("sales/products")]
    public async Task<IActionResult> ProductSales([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetProductSalesAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportProductSales(data), "ProductSales")
            : Ok(data);
    }

    /// <summary>Report 3: Customer purchase history — requires customerId in filter.</summary>
    [HttpGet("sales/customer-history")]
    public async Task<IActionResult> CustomerHistory([FromQuery] ReportFilterDto filter)
    {
        if (string.IsNullOrEmpty(filter.CustomerId))
            return BadRequest(new { message = "customerId is required for this report." });
        return Ok(await _reportService.GetCustomerPurchaseHistoryAsync(filter));
    }

    /// <summary>Report 4: GST filing report with CGST, SGST, IGST breakdowns per invoice.</summary>
    [HttpGet("sales/gst")]
    public async Task<IActionResult> GSTReport([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetGSTReportAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportGSTReport(data), "GSTReport")
            : Ok(data);
    }

    /// <summary>Report 5: Old gold exchange transactions with weight and value given to customers.</summary>
    [HttpGet("sales/old-gold-exchange")]
    public async Task<IActionResult> OldGoldExchange([FromQuery] ReportFilterDto filter)
        => Ok(await _reportService.GetOldGoldExchangeAsync(filter));

    // ── INVENTORY REPORTS ─────────────────────────────────────────────────

    /// <summary>Report 6: Current stock levels with weight and live market value per product.</summary>
    [HttpGet("inventory/current-stock")]
    public async Task<IActionResult> CurrentStock([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetCurrentStockAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportCurrentStock(data), "CurrentStock")
            : Ok(data);
    }

    /// <summary>Report 7: Stock movement ledger — all in/out transactions with dates and references.</summary>
    [HttpGet("inventory/stock-movement")]
    public async Task<IActionResult> StockMovement([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetStockMovementAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportStockMovement(data), "StockMovement")
            : Ok(data);
    }

    /// <summary>Report 8: Inventory valuation at live gold/silver prices, broken down by category.</summary>
    [HttpGet("inventory/valuation")]
    public async Task<IActionResult> Valuation([FromQuery] ReportFilterDto filter)
        => Ok(await _reportService.GetValuationAsync(filter));

    /// <summary>Report 9: Slow-moving items not sold in X days (default 90). Pass slowMovingDays=N to override.</summary>
    [HttpGet("inventory/slow-moving")]
    public async Task<IActionResult> SlowMoving([FromQuery] ReportFilterDto filter)
        => Ok(await _reportService.GetSlowMovingAsync(filter));

    // ── GOLD LOAN REPORTS ─────────────────────────────────────────────────

    /// <summary>Report 10: All active loans with accrued interest and overdue flag, calculated as of today.</summary>
    [HttpGet("loans/active")]
    public async Task<IActionResult> ActiveLoans([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetActiveLoansAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportActiveLoans(data), "ActiveLoans")
            : Ok(data);
    }

    /// <summary>Report 11: Loan repayment history with principal/interest/penalty allocation per receipt.</summary>
    [HttpGet("loans/repayment-history")]
    public async Task<IActionResult> RepaymentHistory([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetRepaymentHistoryAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportRepaymentHistory(data), "RepaymentHistory")
            : Ok(data);
    }

    /// <summary>Report 12: Outstanding balance as of a given date — what every customer currently owes.</summary>
    [HttpGet("loans/outstanding")]
    public async Task<IActionResult> OutstandingBalance([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetOutstandingBalanceAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportOutstandingBalance(data), "OutstandingBalance")
            : Ok(data);
    }

    /// <summary>Report 13: Defaulted and at-risk loans with risk level (AtRisk/Critical/Defaulted).</summary>
    [HttpGet("loans/defaulted")]
    public async Task<IActionResult> DefaultedLoans([FromQuery] ReportFilterDto filter)
        => Ok(await _reportService.GetDefaultedLoansAsync(filter));

    /// <summary>Report 14: Monthly interest and penalty income collected in the period.</summary>
    [HttpGet("loans/interest-income")]
    public async Task<IActionResult> InterestIncome([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetInterestIncomeAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportInterestIncome(data), "InterestIncome")
            : Ok(data);
    }

    /// <summary>Report 15: Full loan history for one customer — requires customerId in filter.</summary>
    [HttpGet("loans/customer-history")]
    public async Task<IActionResult> CustomerLoanHistory([FromQuery] ReportFilterDto filter)
    {
        if (string.IsNullOrEmpty(filter.CustomerId))
            return BadRequest(new { message = "customerId is required." });
        return Ok(await _reportService.GetCustomerLoanHistoryAsync(filter));
    }

    /// <summary>Report 16: All gold currently held as collateral, with current market value vs loan principal.</summary>
    [HttpGet("loans/pledged-gold")]
    public async Task<IActionResult> PledgedGold([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetPledgedGoldAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportPledgedGold(data), "PledgedGold")
            : Ok(data);
    }

    // ── FINANCIAL REPORTS ─────────────────────────────────────────────────

    /// <summary>Report 17: Profit & Loss with gross sales, COGS, making income, interest income, and margins.</summary>
    [HttpGet("financial/profit-loss")]
    public async Task<IActionResult> ProfitLoss([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetProfitLossAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportProfitLoss(data), "ProfitAndLoss")
            : Ok(data);
    }

    /// <summary>Report 18: Cash flow summary with running balance — sales receipts, loan disbursements, repayments.</summary>
    [HttpGet("financial/cash-flow")]
    public async Task<IActionResult> CashFlow([FromQuery] ReportFilterDto filter)
    {
        var data = await _reportService.GetCashFlowAsync(filter);
        return filter.ExportFormat.ToLower() == "xlsx"
            ? ExcelFile(_excelService.ExportCashFlow(data), "CashFlow")
            : Ok(data);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static FileContentResult ExcelFile(byte[] bytes, string name)
        => new(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = $"{name}_{DateTime.Today:yyyyMMdd}.xlsx"
        };
}

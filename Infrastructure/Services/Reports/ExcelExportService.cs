using OfficeOpenXml;
using OfficeOpenXml.Style;
using ShreeJewellers.Application.DTOs.Reports;
using System.Drawing;

namespace ShreeJewellers.Infrastructure.Services.Reports;

public interface IExcelExportService
{
    byte[] ExportSalesSummary(SalesSummaryReportDto data);
    byte[] ExportProductSales(ProductSalesReportDto data);
    byte[] ExportGSTReport(GSTReportDto data);
    byte[] ExportCurrentStock(CurrentStockReportDto data);
    byte[] ExportStockMovement(StockMovementReportDto data);
    byte[] ExportActiveLoans(ActiveLoansSummaryDto data);
    byte[] ExportRepaymentHistory(LoanRepaymentHistoryDto data);
    byte[] ExportOutstandingBalance(OutstandingBalanceReportDto data);
    byte[] ExportInterestIncome(InterestIncomeReportDto data);
    byte[] ExportPledgedGold(PledgedGoldReportDto data);
    byte[] ExportProfitLoss(ProfitLossReportDto data);
    byte[] ExportCashFlow(CashFlowReportDto data);
}

/// <summary>
/// Generates Excel workbooks using EPPlus.
/// All exports share consistent styling: gold header, alternating row colours, auto-fit columns.
/// </summary>
public class ExcelExportService : IExcelExportService
{
    static ExcelExportService() =>
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

    // ── Report 1: Sales Summary ───────────────────────────────────────────

    public byte[] ExportSalesSummary(SalesSummaryReportDto d)
    {
        using var pkg = new ExcelPackage();
        BuildSummarySheet(pkg, d);
        BuildSheetByCategory(pkg, d.ByCategory);
        BuildSheetByPayment(pkg, d.ByPaymentMode);
        return pkg.GetAsByteArray();
    }

    private static void BuildSummarySheet(ExcelPackage pkg, SalesSummaryReportDto d)
    {
        var ws = CreateSheet(pkg, "Sales Summary");
        AddTitle(ws, $"Sales Summary: {d.FromDate:dd-MMM-yyyy} to {d.ToDate:dd-MMM-yyyy}", 1, 5);
        AddKpiRow(ws, 3, "Total Orders", d.TotalOrders.ToString());
        AddKpiRow(ws, 4, "Gross Sales",  $"₹{d.TotalGrossAmount:N2}");
        AddKpiRow(ws, 5, "Total Tax",    $"₹{d.TotalTax:N2}");
        AddKpiRow(ws, 6, "Net Amount",   $"₹{d.TotalNetAmount:N2}");
        AddKpiRow(ws, 7, "Old Gold Exchange", $"₹{d.TotalOldGoldExchange:N2}");
        AddKpiRow(ws, 8, "Balance Due",  $"₹{d.TotalBalanceDue:N2}");

        int row = 10;
        AddHeaders(ws, row, "Period", "Orders", "Gross Amount", "Net Amount", "Tax Amount");
        foreach (var r in d.ByPeriod)
        {
            row++;
            ws.Cells[row, 1].Value = r.Period;
            ws.Cells[row, 2].Value = r.Orders;
            ws.Cells[row, 3].Value = r.GrossAmount;
            ws.Cells[row, 4].Value = r.NetAmount;
            ws.Cells[row, 5].Value = r.TaxAmount;
            ApplyAlternate(ws, row, 5);
        }
        FormatCurrencyColumns(ws, 11, row, 3, 4, 5);
        AutoFit(ws, 5);
    }

    private static void BuildSheetByCategory(ExcelPackage pkg, List<SalesByCategoryDto> data)
    {
        var ws = CreateSheet(pkg, "By Category");
        AddHeaders(ws, 1, "Category", "Orders", "Items Sold", "Weight (g)", "Net Amount", "Share %");
        int row = 1;
        foreach (var r in data)
        {
            row++;
            ws.Cells[row, 1].Value = r.CategoryName;
            ws.Cells[row, 2].Value = r.Orders;
            ws.Cells[row, 3].Value = r.ItemsSold;
            ws.Cells[row, 4].Value = r.TotalWeightGrams;
            ws.Cells[row, 5].Value = r.TotalNetAmount;
            ws.Cells[row, 6].Value = $"{r.Percent}%";
            ApplyAlternate(ws, row, 6);
        }
        FormatCurrencyColumns(ws, 2, row, 5);
        AutoFit(ws, 6);
    }

    private static void BuildSheetByPayment(ExcelPackage pkg, List<SalesByPaymentModeDto> data)
    {
        var ws = CreateSheet(pkg, "By Payment Mode");
        AddHeaders(ws, 1, "Payment Mode", "Count", "Amount", "Share %");
        int row = 1;
        foreach (var r in data)
        {
            row++;
            ws.Cells[row, 1].Value = r.PaymentMode;
            ws.Cells[row, 2].Value = r.Count;
            ws.Cells[row, 3].Value = r.Amount;
            ws.Cells[row, 4].Value = $"{r.Percent}%";
            ApplyAlternate(ws, row, 4);
        }
        FormatCurrencyColumns(ws, 2, row, 3);
        AutoFit(ws, 4);
    }

    // ── Report 2: Product Sales ───────────────────────────────────────────

    public byte[] ExportProductSales(ProductSalesReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Product Sales");
        AddTitle(ws, $"Product Sales: {d.FromDate:dd-MMM-yyyy} to {d.ToDate:dd-MMM-yyyy}", 1, 9);
        int row = 2;
        AddHeaders(ws, row, "SKU", "Product", "Category", "Purity", "Qty", "Weight (g)",
            "Revenue ₹", "Making ₹", "Margin %");

        foreach (var r in d.Rows)
        {
            row++;
            ws.Cells[row, 1].Value = r.SKUCode;
            ws.Cells[row, 2].Value = r.ProductName;
            ws.Cells[row, 3].Value = r.CategoryName;
            ws.Cells[row, 4].Value = r.Purity;
            ws.Cells[row, 5].Value = r.QuantitySold;
            ws.Cells[row, 6].Value = r.WeightSoldGrams;
            ws.Cells[row, 7].Value = r.TotalRevenue;
            ws.Cells[row, 8].Value = r.TotalMakingCharges;
            ws.Cells[row, 9].Value = r.MarginPercent;
            ApplyAlternate(ws, row, 9);
        }
        FormatCurrencyColumns(ws, 3, row, 7, 8);
        AutoFit(ws, 9);
        return pkg.GetAsByteArray();
    }

    // ── Report 4: GST ─────────────────────────────────────────────────────

    public byte[] ExportGSTReport(GSTReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "GST Report");
        AddTitle(ws, $"GST Report: {d.FromDate:dd-MMM-yyyy} to {d.ToDate:dd-MMM-yyyy}", 1, 7);
        AddKpiRow(ws, 3, "Total Taxable Value", $"₹{d.TotalTaxableValue:N2}");
        AddKpiRow(ws, 4, "Total CGST",          $"₹{d.TotalCGST:N2}");
        AddKpiRow(ws, 5, "Total SGST",          $"₹{d.TotalSGST:N2}");
        AddKpiRow(ws, 6, "Total IGST",          $"₹{d.TotalIGST:N2}");
        AddKpiRow(ws, 7, "Total GST",           $"₹{d.TotalGST:N2}");
        int row = 9;
        AddHeaders(ws, row, "Invoice No", "Date", "Customer", "Taxable Amt", "GST%", "CGST", "SGST", "IGST");
        foreach (var r in d.Lines)
        {
            row++;
            ws.Cells[row, 1].Value = r.InvoiceNumber;
            ws.Cells[row, 2].Value = r.InvoiceDate.ToString("dd-MMM-yyyy");
            ws.Cells[row, 3].Value = r.CustomerName ?? "Walk-in";
            ws.Cells[row, 4].Value = r.TaxableAmount;
            ws.Cells[row, 5].Value = $"{r.GSTRate}%";
            ws.Cells[row, 6].Value = r.CGSTAmount;
            ws.Cells[row, 7].Value = r.SGSTAmount;
            ws.Cells[row, 8].Value = r.IGSTAmount;
            ApplyAlternate(ws, row, 8);
        }
        FormatCurrencyColumns(ws, 10, row, 4, 6, 7, 8);
        AutoFit(ws, 8);
        return pkg.GetAsByteArray();
    }

    // ── Report 6: Current Stock ───────────────────────────────────────────

    public byte[] ExportCurrentStock(CurrentStockReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Current Stock");
        AddTitle(ws, $"Stock Report as of {d.AsOf:dd-MMM-yyyy HH:mm}", 1, 9);
        int row = 2;
        AddHeaders(ws, row, "SKU", "Product", "Category", "Purity", "Qty", "Reorder", "Wt/Piece (g)",
            "Total Wt (g)", "Market Value ₹");

        foreach (var r in d.Rows)
        {
            row++;
            ws.Cells[row, 1].Value = r.SKUCode;
            ws.Cells[row, 2].Value = r.ProductName;
            ws.Cells[row, 3].Value = r.CategoryName;
            ws.Cells[row, 4].Value = r.Purity;
            ws.Cells[row, 5].Value = r.StockQty;
            ws.Cells[row, 6].Value = r.ReorderLevel;
            ws.Cells[row, 7].Value = r.NetWeightPerPiece;
            ws.Cells[row, 8].Value = r.TotalWeightGrams;
            ws.Cells[row, 9].Value = r.TotalMarketValue;
            ApplyAlternate(ws, row, 9);
            if (r.IsLowStock) ws.Cells[row, 5].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 0, 0));
        }
        FormatCurrencyColumns(ws, 3, row, 9);
        AutoFit(ws, 9);
        return pkg.GetAsByteArray();
    }

    // ── Report 7: Stock Movement ──────────────────────────────────────────

    public byte[] ExportStockMovement(StockMovementReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Stock Movement");
        int row = 1;
        AddHeaders(ws, row, "Date", "Product", "SKU", "Type", "Qty", "Weight (g)", "Rate/g", "Value ₹", "Reference", "Staff");
        foreach (var r in d.Rows)
        {
            row++;
            ws.Cells[row, 1].Value = r.TransactionDate.ToString("dd-MMM-yyyy HH:mm");
            ws.Cells[row, 2].Value = r.ProductName;
            ws.Cells[row, 3].Value = r.SKUCode;
            ws.Cells[row, 4].Value = r.TransactionType;
            ws.Cells[row, 5].Value = r.Quantity;
            ws.Cells[row, 6].Value = r.WeightGrams;
            ws.Cells[row, 7].Value = r.RatePerGram;
            ws.Cells[row, 8].Value = r.TotalValue;
            ws.Cells[row, 9].Value = r.ReferenceNo;
            ws.Cells[row, 10].Value = r.CreatedBy;
            ApplyAlternate(ws, row, 10);
        }
        FormatCurrencyColumns(ws, 2, row, 8);
        AutoFit(ws, 10);
        return pkg.GetAsByteArray();
    }

    // ── Report 10: Active Loans ───────────────────────────────────────────

    public byte[] ExportActiveLoans(ActiveLoansSummaryDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Active Loans");
        AddTitle(ws, $"Active Loans as of {d.AsOf:dd-MMM-yyyy}", 1, 12);
        AddKpiRow(ws, 3, "Total Loans",     d.TotalActiveLoans.ToString());
        AddKpiRow(ws, 4, "Total Principal", $"₹{d.TotalPrincipalOutstanding:N2}");
        AddKpiRow(ws, 5, "Total Interest",  $"₹{d.TotalAccruedInterest:N2}");
        AddKpiRow(ws, 6, "Total Outstanding", $"₹{d.TotalOutstanding:N2}");
        int row = 8;
        AddHeaders(ws, row, "Loan No", "Customer", "Code", "Phone", "Loan Date",
            "Maturity", "Principal ₹", "Interest ₹", "Penalty ₹", "Total Owed ₹", "Status", "Overdue?");
        foreach (var r in d.Rows)
        {
            row++;
            ws.Cells[row, 1].Value = r.LoanNumber;
            ws.Cells[row, 2].Value = r.CustomerName;
            ws.Cells[row, 3].Value = r.CustomerCode;
            ws.Cells[row, 4].Value = r.CustomerPhone;
            ws.Cells[row, 5].Value = r.LoanDate.ToString("dd-MMM-yyyy");
            ws.Cells[row, 6].Value = r.MaturityDate.ToString("dd-MMM-yyyy");
            ws.Cells[row, 7].Value = r.PrincipalRemaining;
            ws.Cells[row, 8].Value = r.AccruedInterest;
            ws.Cells[row, 9].Value = r.PenaltyInterest;
            ws.Cells[row, 10].Value = r.TotalOutstanding;
            ws.Cells[row, 11].Value = r.LoanStatus;
            ws.Cells[row, 12].Value = r.IsOverdue ? $"Yes ({r.DaysOverdue}d)" : "No";
            ApplyAlternate(ws, row, 12);
            if (r.IsOverdue) ws.Cells[row, 12].Style.Font.Color.SetColor(System.Drawing.Color.Red);
        }
        FormatCurrencyColumns(ws, 9, row, 7, 8, 9, 10);
        AutoFit(ws, 12);
        return pkg.GetAsByteArray();
    }

    // ── Remaining exports (follow identical pattern) ──────────────────────

    public byte[] ExportRepaymentHistory(LoanRepaymentHistoryDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Repayment History");
        int row = 1;
        AddHeaders(ws, row, "Receipt No", "Date", "Loan No", "Customer", "Total Paid ₹",
            "Principal ₹", "Interest ₹", "Penalty ₹", "Mode", "Balance After ₹");
        foreach (var r in d.Rows)
        {
            row++;
            ws.Cells[row, 1].Value = r.ReceiptNumber;
            ws.Cells[row, 2].Value = r.RepaymentDate.ToString("dd-MMM-yyyy");
            ws.Cells[row, 3].Value = r.LoanNumber;
            ws.Cells[row, 4].Value = r.CustomerName;
            ws.Cells[row, 5].Value = r.AmountPaid;
            ws.Cells[row, 6].Value = r.PrincipalComponent;
            ws.Cells[row, 7].Value = r.InterestComponent;
            ws.Cells[row, 8].Value = r.PenaltyAmount;
            ws.Cells[row, 9].Value = r.PaymentMode;
            ws.Cells[row, 10].Value = r.PrincipalBalanceAfter;
            ApplyAlternate(ws, row, 10);
        }
        FormatCurrencyColumns(ws, 2, row, 5, 6, 7, 8, 10);
        AutoFit(ws, 10);
        return pkg.GetAsByteArray();
    }

    public byte[] ExportOutstandingBalance(OutstandingBalanceReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Outstanding Balance");
        int row = 1;
        AddHeaders(ws, row, "Loan No", "Customer", "Code", "Loan Date", "Maturity",
            "Original ₹", "Principal Rem ₹", "Interest ₹", "Penalty ₹", "Total Owed ₹", "Status");
        foreach (var r in d.Rows)
        {
            row++;
            ws.Cells[row, 1].Value  = r.LoanNumber;
            ws.Cells[row, 2].Value  = r.CustomerName;
            ws.Cells[row, 3].Value  = r.CustomerCode;
            ws.Cells[row, 4].Value  = r.LoanDate.ToString("dd-MMM-yyyy");
            ws.Cells[row, 5].Value  = r.MaturityDate.ToString("dd-MMM-yyyy");
            ws.Cells[row, 6].Value  = r.OriginalPrincipal;
            ws.Cells[row, 7].Value  = r.PrincipalRemaining;
            ws.Cells[row, 8].Value  = r.AccruedInterest;
            ws.Cells[row, 9].Value  = r.PenaltyInterest;
            ws.Cells[row, 10].Value = r.TotalOwed;
            ws.Cells[row, 11].Value = r.Status;
            ApplyAlternate(ws, row, 11);
        }
        FormatCurrencyColumns(ws, 2, row, 6, 7, 8, 9, 10);
        AutoFit(ws, 11);
        return pkg.GetAsByteArray();
    }

    public byte[] ExportInterestIncome(InterestIncomeReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Interest Income");
        AddKpiRow(ws, 1, "Total Interest Earned", $"₹{d.TotalInterestEarned:N2}");
        AddKpiRow(ws, 2, "Total Penalty Earned",  $"₹{d.TotalPenaltyEarned:N2}");
        int row = 4;
        AddHeaders(ws, row, "Month", "Active Loans", "Interest ₹", "Penalty ₹", "Total Collected ₹");
        foreach (var r in d.ByMonth) { row++; ws.Cells[row, 1].Value = r.Month; ws.Cells[row, 2].Value = r.ActiveLoans; ws.Cells[row, 3].Value = r.InterestCollected; ws.Cells[row, 4].Value = r.PenaltyCollected; ws.Cells[row, 5].Value = r.TotalCollected; ApplyAlternate(ws, row, 5); }
        FormatCurrencyColumns(ws, 5, row, 3, 4, 5);
        AutoFit(ws, 5);
        return pkg.GetAsByteArray();
    }

    public byte[] ExportPledgedGold(PledgedGoldReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Pledged Gold");
        int row = 1;
        AddHeaders(ws, row, "Loan No", "Customer", "Code", "Loan Date", "Principal ₹",
            "Total Weight (g)", "Purity", "Gold Value at Deposit ₹", "Current Value ₹", "LTV%", "Status");
        foreach (var r in d.Rows) { row++; ws.Cells[row, 1].Value = r.LoanNumber; ws.Cells[row, 2].Value = r.CustomerName; ws.Cells[row, 3].Value = r.CustomerCode; ws.Cells[row, 4].Value = r.LoanDate.ToString("dd-MMM-yyyy"); ws.Cells[row, 5].Value = r.LoanPrincipal; ws.Cells[row, 6].Value = r.TotalPledgedWeight; ws.Cells[row, 7].Value = r.GoldPurity; ws.Cells[row, 8].Value = r.GoldValueAtDeposit; ws.Cells[row, 9].Value = r.CurrentMarketValue; ws.Cells[row, 10].Value = r.CurrentLTV; ws.Cells[row, 11].Value = r.LoanStatus; ApplyAlternate(ws, row, 11); }
        FormatCurrencyColumns(ws, 2, row, 5, 8, 9);
        AutoFit(ws, 11);
        return pkg.GetAsByteArray();
    }

    public byte[] ExportProfitLoss(ProfitLossReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Profit & Loss");
        AddTitle(ws, $"P&L: {d.FromDate:dd-MMM-yyyy} to {d.ToDate:dd-MMM-yyyy}", 1, 2);
        AddKpiRow(ws, 3, "Gross Sales",       $"₹{d.GrossSalesRevenue:N2}");
        AddKpiRow(ws, 4, "Net Sales",         $"₹{d.NetSalesRevenue:N2}");
        AddKpiRow(ws, 5, "Making Charges",    $"₹{d.MakingChargeIncome:N2}");
        AddKpiRow(ws, 6, "Interest Income",   $"₹{d.InterestIncome:N2}");
        AddKpiRow(ws, 7, "Penalty Income",    $"₹{d.PenaltyIncome:N2}");
        AddKpiRow(ws, 8, "Total Revenue",     $"₹{d.TotalRevenue:N2}");
        AddKpiRow(ws, 9, "COGS",              $"₹{d.CostOfGoldSold:N2}");
        AddKpiRow(ws, 10, "Gross Profit",     $"₹{d.GrossProfit:N2}");
        AddKpiRow(ws, 11, "Gross Margin %",   $"{d.GrossProfitMarginPercent:N2}%");
        int row = 13;
        AddHeaders(ws, row, "Period", "Revenue ₹", "COGS ₹", "Gross Profit ₹", "Interest ₹", "Total Income ₹");
        foreach (var r in d.ByPeriod) { row++; ws.Cells[row, 1].Value = r.Period; ws.Cells[row, 2].Value = r.Revenue; ws.Cells[row, 3].Value = r.COGS; ws.Cells[row, 4].Value = r.GrossProfit; ws.Cells[row, 5].Value = r.InterestIncome; ws.Cells[row, 6].Value = r.TotalIncome; ApplyAlternate(ws, row, 6); }
        FormatCurrencyColumns(ws, 14, row, 2, 3, 4, 5, 6);
        AutoFit(ws, 6);
        return pkg.GetAsByteArray();
    }

    public byte[] ExportCashFlow(CashFlowReportDto d)
    {
        using var pkg = new ExcelPackage();
        var ws = CreateSheet(pkg, "Cash Flow");
        AddKpiRow(ws, 1, "Total Cash In",  $"₹{d.TotalCashIn:N2}");
        AddKpiRow(ws, 2, "Total Cash Out", $"₹{d.TotalCashOut:N2}");
        AddKpiRow(ws, 3, "Net Cash Flow",  $"₹{d.NetCashFlow:N2}");
        int row = 5;
        AddHeaders(ws, row, "Date", "Description", "Type", "Cash In ₹", "Cash Out ₹", "Running Balance ₹");
        foreach (var r in d.Rows) { row++; ws.Cells[row, 1].Value = r.Date.ToString("dd-MMM-yyyy"); ws.Cells[row, 2].Value = r.Description; ws.Cells[row, 3].Value = r.Type; ws.Cells[row, 4].Value = r.CashIn; ws.Cells[row, 5].Value = r.CashOut; ws.Cells[row, 6].Value = r.RunningBalance; ApplyAlternate(ws, row, 6); if (r.RunningBalance < 0) ws.Cells[row, 6].Style.Font.Color.SetColor(System.Drawing.Color.Red); }
        FormatCurrencyColumns(ws, 6, row, 4, 5, 6);
        AutoFit(ws, 6);
        return pkg.GetAsByteArray();
    }

    // ── Styling helpers ───────────────────────────────────────────────────

    private static ExcelWorksheet CreateSheet(ExcelPackage pkg, string name)
    {
        var ws = pkg.Workbook.Worksheets.Add(name);
        ws.DefaultColWidth = 16;
        return ws;
    }

    private static void AddTitle(ExcelWorksheet ws, string title, int row, int cols)
    {
        ws.Cells[row, 1, row, cols].Merge = true;
        ws.Cells[row, 1].Value = title;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.Font.Size = 12;
        ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(184, 134, 11)); // Gold
        ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
    }

    private static void AddHeaders(ExcelWorksheet ws, int row, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[row, i + 1].Value = headers[i];
            ws.Cells[row, i + 1].Style.Font.Bold = true;
            ws.Cells[row, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(28, 28, 46));
            ws.Cells[row, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
        }
    }

    private static void AddKpiRow(ExcelWorksheet ws, int row, string label, string value)
    {
        ws.Cells[row, 1].Value = label;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 2].Value = value;
    }

    private static void ApplyAlternate(ExcelWorksheet ws, int row, int cols)
    {
        if (row % 2 == 0)
        {
            ws.Cells[row, 1, row, cols].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1, row, cols].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(253, 248, 238));
        }
    }

    private static void FormatCurrencyColumns(ExcelWorksheet ws, int startRow, int endRow, params int[] cols)
    {
        foreach (var col in cols)
            ws.Cells[startRow, col, endRow, col].Style.Numberformat.Format = "#,##0.00";
    }

    private static void AutoFit(ExcelWorksheet ws, int cols)
    {
        for (int i = 1; i <= cols; i++)
            ws.Column(i).AutoFit();
    }
}

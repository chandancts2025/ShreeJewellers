using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShreeJewellers.Application.DTOs;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;

namespace ShreeJewellers.Infrastructure.Services;

public interface IInvoiceGeneratorService
{
    Task<string> GenerateAsync(SalesOrder order);
}

/// <summary>
/// Generates GST-compliant PDF invoices using QuestPDF.
/// Includes: shop letterhead, BIS hallmark details, GST breakdown (CGST/SGST/IGST),
/// HSN codes, product weight details, QR code, and payment summary.
/// </summary>
public class InvoiceGeneratorService : IInvoiceGeneratorService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileUploadService _fileService;
    private readonly IConfiguration _config;
    private readonly IBarcodeService _barcodeService;
    private readonly ILogger<InvoiceGeneratorService> _logger;

    // Colour palette
    private static readonly string GoldDark  = Color.FromHex("#B8860B");
    private static readonly string DarkSlate = Color.FromHex("#1C1C2E");
    private static readonly string LightBg   = Color.FromHex("#FDF8EE");

    public InvoiceGeneratorService(ApplicationDbContext db, IFileUploadService fileService,
        IConfiguration config, IBarcodeService barcodeService, ILogger<InvoiceGeneratorService> logger)
    {
        _db = db; _fileService = fileService;
        _config = config; _barcodeService = barcodeService; _logger = logger;

        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GenerateAsync(SalesOrder order)
    {
        var shopSettings = await GetShopSettingsAsync();
        var customer     = order.Customer;
        var bytes        = GeneratePdf(order, shopSettings, customer);
        var fileName     = $"Invoice_{order.OrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        var relativePath = await _fileService.UploadBase64Async(
            Convert.ToBase64String(bytes), "pdf", "invoices", fileName);

        _logger.LogInformation("Invoice generated: {Path}", relativePath);
        return relativePath;
    }

    private byte[] GeneratePdf(SalesOrder order, ShopSettings shop, ApplicationUser? customer)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                page.Header().Element(BuildHeader(shop, order));
                page.Content().Element(BuildContent(order, shop, customer));
                page.Footer().Element(BuildFooter(shop));
            });
        }).GeneratePdf();
    }

    // ── Header ────────────────────────────────────────────────────────────

    private Action<IContainer> BuildHeader(ShopSettings shop, SalesOrder order) => c => c
        .BorderBottom(1).BorderColor(GoldDark.ToString())
        .PaddingBottom(8)
        .Row(row =>
        {
            // Left: Shop details
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(shop.ShopName)
                    .FontSize(20).Bold().FontColor(DarkSlate.ToString());
                col.Item().Text(shop.Address).FontSize(8).FontColor(Colors.Grey.Darken2);
                col.Item().Text($"GST: {shop.GSTNumber} | {shop.Phone}")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingTop(4)
                    .Text("TAX INVOICE").Bold().FontSize(11).FontColor(GoldDark.ToString());
            });

            // Right: Invoice details box
            row.ConstantItem(180).Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(8).Column(col =>
                {
                    col.Item().Row(r => {
                        r.RelativeItem().Text("Invoice No:").Bold();
                        r.RelativeItem().Text(order.OrderNumber).FontColor(GoldDark.ToString());
                    });
                    col.Item().Row(r => {
                        r.RelativeItem().Text("Date:").Bold();
                        r.RelativeItem().Text(order.OrderDate.ToString("dd-MMM-yyyy"));
                    });
                    col.Item().Row(r => {
                        r.RelativeItem().Text("Payment:").Bold();
                        r.RelativeItem().Text(order.PaymentMode.ToString());
                    });
                    col.Item().Row(r => {
                        r.RelativeItem().Text("Status:").Bold();
                        r.RelativeItem().Text(order.PaymentStatus.ToString())
                            .FontColor(order.PaymentStatus == PaymentStatus.Paid
                                ? Colors.Green.Darken1 : Colors.Orange.Darken1);
                    });
                });
        });

    // ── Content ───────────────────────────────────────────────────────────

    private Action<IContainer> BuildContent(SalesOrder order, ShopSettings shop,
        ApplicationUser? customer) => c => c.Column(col =>
    {
        // Customer section
        col.Item().PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Column(inner =>
            {
                inner.Item().Text("Bill To:").Bold().FontSize(9);
                inner.Item().Text(customer is not null
                    ? $"{customer.FirstName} {customer.LastName}"
                    : (order.Notes?.Split('|').FirstOrDefault()?.Replace("Walk-in: ", "") ?? "Walk-in Customer"))
                    .Bold();
                if (customer is not null)
                {
                    inner.Item().Text($"{customer.AddressLine1}, {customer.City} - {customer.PinCode}");
                    inner.Item().Text($"Phone: {customer.PhoneNumber}");
                    inner.Item().Text($"Customer Code: {customer.CustomerCode}");
                }
            });
        });

        // Items table
        col.Item().PaddingTop(10).Table(table =>
        {
            // Column widths
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(22);   // #
                cols.RelativeColumn(3);    // Description
                cols.ConstantColumn(35);   // HSN
                cols.ConstantColumn(40);   // Weight
                cols.ConstantColumn(48);   // Rate/g
                cols.ConstantColumn(45);   // Making
                cols.ConstantColumn(30);   // Tax%
                cols.ConstantColumn(55);   // Amount
            });

            // Header row
            static IContainer HeaderCell(IContainer c) => c
                .DefaultTextStyle(x => x.Bold().FontSize(8).FontColor(Colors.White))
                .Background(DarkSlate.ToString()).Padding(4).AlignCenter();

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Item Description");
                header.Cell().Element(HeaderCell).Text("HSN");
                header.Cell().Element(HeaderCell).Text("Wt (g)");
                header.Cell().Element(HeaderCell).Text("Rate/g ₹");
                header.Cell().Element(HeaderCell).Text("Making ₹");
                header.Cell().Element(HeaderCell).Text("GST%");
                header.Cell().Element(HeaderCell).Text("Amount ₹");
            });

            // Data rows
            var items = order.Items.ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var item  = items[i];
                var isOdd = i % 2 == 0;
                var bg    = isOdd ? Colors.White.ToString() : LightBg.ToString();

                static IContainer DataCell(IContainer c, string bg) =>
                    c.Background(bg).Padding(3).AlignCenter();

                table.Cell().Element(c => DataCell(c, bg)).Text($"{i + 1}");
                table.Cell().Element(c => c.Background(bg).Padding(3)).Column(inner =>
                {
                    inner.Item().Text(item.Product?.Name ?? "").Bold().FontSize(8);
                    inner.Item().Text($"{item.Product?.Purity} | Wt: {item.WeightGrams:F3}g | Qty: {item.Quantity}")
                        .FontSize(7).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(item.Product?.HallmarkNumber))
                        inner.Item().Text($"Hallmark: {item.Product.HallmarkNumber}").FontSize(7);
                });
                table.Cell().Element(c => DataCell(c, bg)).Text(item.Product?.HSNCode ?? "");
                table.Cell().Element(c => DataCell(c, bg)).Text($"{item.WeightGrams:F3}");
                table.Cell().Element(c => DataCell(c, bg)).Text($"₹{item.RatePerGram:N2}");
                table.Cell().Element(c => DataCell(c, bg)).Text($"₹{item.MakingCharges:N2}");
                table.Cell().Element(c => DataCell(c, bg)).Text($"{item.TaxPercent}%");
                table.Cell().Element(c => DataCell(c, bg)).Text($"₹{item.LineTotal:N2}");
            }
        });

        // Totals section
        col.Item().PaddingTop(8).Row(row =>
        {
            // Left: tax breakdown
            row.RelativeItem().Column(inner =>
            {
                inner.Item().Text("Tax Breakup:").Bold().FontSize(8);
                if (order.CGSTAmount > 0)
                {
                    inner.Item().Text($"CGST: ₹{order.CGSTAmount:N2}").FontSize(8);
                    inner.Item().Text($"SGST: ₹{order.SGSTAmount:N2}").FontSize(8);
                }
                if (order.IGSTAmount > 0)
                    inner.Item().Text($"IGST: ₹{order.IGSTAmount:N2}").FontSize(8);
                if (order.OldGoldExchangeValue > 0)
                    inner.Item().Text($"Old Gold Exchange: -₹{order.OldGoldExchangeValue:N2}")
                        .FontSize(8).FontColor(Colors.Green.Darken1);
                if (order.DiscountAmount > 0)
                    inner.Item().Text($"Discount: -₹{order.DiscountAmount:N2}")
                        .FontSize(8).FontColor(Colors.Green.Darken1);
            });

            // Right: total box
            row.ConstantItem(160).Border(1).BorderColor(GoldDark.ToString())
                .Padding(6).Column(inner =>
            {
                void TotalRow(string label, decimal amount, bool bold = false, string? color = null) =>
                    inner.Item().Row(r =>
                    {
                        var lt = r.RelativeItem().Text(label).FontSize(9);
                        var rt = r.ConstantItem(80).AlignRight().Text($"₹{amount:N2}").FontSize(9);
                        if (bold) { lt.Bold(); rt.Bold(); }
                        if (color is not null) rt.FontColor(color);
                    });

                TotalRow("Subtotal:", order.GrossAmount);
                TotalRow("Tax:", order.TaxAmount);
                if (order.DiscountAmount > 0) TotalRow("Discount:", -order.DiscountAmount, color: Colors.Green.Darken1);
                if (order.OldGoldExchangeValue > 0) TotalRow("Old Gold:", -order.OldGoldExchangeValue, color: Colors.Green.Darken1);
                inner.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(2);
                TotalRow("NET PAYABLE:", order.NetAmount, bold: true, color: GoldDark.ToString());
                TotalRow("Paid:", order.AmountPaid, color: Colors.Green.Darken1);
                if (order.NetAmount - order.AmountPaid > 0)
                    TotalRow("Balance Due:", order.NetAmount - order.AmountPaid,
                        color: Colors.Red.Darken1);
            });
        });

        // Terms
        col.Item().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(4).Column(inner =>
        {
            inner.Item().Text("Terms & Conditions:").Bold().FontSize(7);
            inner.Item().Text("1. Goods once sold cannot be returned without original invoice within 7 days.")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            inner.Item().Text("2. This is a computer-generated invoice and does not require a signature.")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            inner.Item().Text("3. All disputes subject to local jurisdiction.")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    });

    // ── Footer ────────────────────────────────────────────────────────────

    private Action<IContainer> BuildFooter(ShopSettings shop) => c => c
        .BorderTop(1).BorderColor(GoldDark.ToString())
        .PaddingTop(4)
        .Row(row =>
        {
            row.RelativeItem().Text(shop.ShopName).Bold().FontSize(8);
            row.RelativeItem().AlignCenter()
                .Text("Thank you for your business! 💍").FontSize(8)
                .FontColor(GoldDark.ToString());
            row.RelativeItem().AlignRight()
                .Text(t =>
                {
                    t.Span("Page ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" of ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
        });

    // ── Shop Settings ─────────────────────────────────────────────────────

    private async Task<ShopSettings> GetShopSettingsAsync()
    {
        var settings = await _db.AppSettings
            .Where(s => new[] { "ShopName", "ShopAddress", "GSTNumber", "ShopPhone" }
                .Contains(s.SettingKey))
            .ToListAsync();

        string Get(string key, string fallback) =>
            settings.FirstOrDefault(s => s.SettingKey == key)?.SettingValue ?? fallback;

        return new ShopSettings(
            Get("ShopName",    "Shree Jewellers"),
            Get("ShopAddress", ""),
            Get("GSTNumber",   ""),
            Get("ShopPhone",   ""));
    }

    private record ShopSettings(string ShopName, string Address, string GSTNumber, string Phone);

    // QuestPDF doesn't have a Color.FromHex natively — use this helper
    private static string HexColor(string hex) => hex;
}

// Re-export Color helper for QuestPDF
public static class Color
{
    public static string FromHex(string hex) => hex;
}

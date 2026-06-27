using Microsoft.Extensions.Logging;
using QRCoder;

namespace ShreeJewellers.Infrastructure.Services;

public interface IBarcodeService
{
    /// <summary>Generates a QR code PNG as a Base64 string for the given product SKU.</summary>
    string GenerateProductQrCodeBase64(string skuCode, string productName);

    /// <summary>Generates a QR code PNG byte array written to a file path.</summary>
    Task<string> GenerateAndSaveQrCodeAsync(string data, string category, string fileName);

    /// <summary>Generates a compact barcode data string for a product.</summary>
    string BuildBarcodeData(string skuCode, int productId, string purity);
}

/// <summary>
/// Generates QR codes for products using QRCoder.
/// QR codes encode: SKU | ProductId | Purity | ShopCode
/// Scannable on the billing page via camera or USB barcode scanner.
/// </summary>
public class BarcodeService : IBarcodeService
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<BarcodeService> _logger;

    // Shop identifier prefix embedded in all barcodes for authenticity
    private const string ShopPrefix = "SJ";

    public BarcodeService(IFileUploadService fileUploadService, ILogger<BarcodeService> logger)
    {
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    // ── Generate QR Code Base64 ───────────────────────────────────────────

    public string GenerateProductQrCodeBase64(string skuCode, string productName)
    {
        var data = $"{ShopPrefix}|{skuCode}|{productName}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData  = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
        using var qrCode      = new PngByteQRCode(qrCodeData);

        var bytes = qrCode.GetGraphic(
            pixelsPerModule: 5,
            darkColorRgba:   new byte[] { 30,  30,  30,  255 },   // Near-black
            lightColorRgba:  new byte[] { 255, 255, 255, 255 });  // White background

        return Convert.ToBase64String(bytes);
    }

    // ── Generate and Save QR Code File ────────────────────────────────────

    public async Task<string> GenerateAndSaveQrCodeAsync(
        string data, string category, string fileName)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData  = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
        using var qrCode      = new PngByteQRCode(qrCodeData);
        var bytes = qrCode.GetGraphic(4);

        var base64   = Convert.ToBase64String(bytes);
        var filePath = await _fileUploadService.UploadBase64Async(
            base64, "png", category, fileName);

        return filePath;
    }

    // ── Build Barcode Data String ─────────────────────────────────────────

    public string BuildBarcodeData(string skuCode, int productId, string purity)
        => $"{ShopPrefix}|{productId}|{skuCode}|{purity}";
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Infrastructure.Services;
using System.Security.Claims;

// ═════════════════════════════════════════════════════════════════════════════
// PRODUCT CONTROLLER
// ═════════════════════════════════════════════════════════════════════════════

namespace ShreeJewellers.API.Controllers;

/// <summary>
/// Manages jewellery product catalog: CRUD, search, QR code generation.
/// Public read access for product browsing; write access requires Admin/Staff.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
        => _productService = productService;

    /// <summary>Search products with optional filters (category, purity, low-stock). Public endpoint.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetProducts([FromQuery] ProductSearchDto filter)
    {
        var (items, total) = await _productService.SearchAsync(filter);
        return Ok(new { total, page = filter.Page, pageSize = filter.PageSize, data = items });
    }

    /// <summary>Get full product details by ID, including live price calculation.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProduct(int id)
        => Ok(await _productService.GetByIdAsync(id));

    /// <summary>Lookup product by SKU code — used by barcode scanner on billing screen.</summary>
    [HttpGet("by-sku/{sku}")]
    [Authorize]
    [ProducesResponseType(typeof(ProductResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBySku(string sku)
    {
        var product = await _productService.GetBySkuAsync(sku);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Get all product categories with product count.</summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryDto>), 200)]
    public async Task<IActionResult> GetCategories()
        => Ok(await _productService.GetCategoriesAsync());

    /// <summary>Products at or below reorder level — Admin/Staff dashboard alert.</summary>
    [HttpGet("low-stock")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(List<ProductSummaryDto>), 200)]
    public async Task<IActionResult> GetLowStock()
        => Ok(await _productService.GetLowStockAsync());

    /// <summary>Create a new product with optional image upload (Base64).</summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(ProductResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
    {
        var userId  = GetUserId();
        var product = await _productService.CreateAsync(dto, userId);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    /// <summary>Update product details. SKU change will fail if duplicate exists.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(ProductResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
    {
        if (dto.Id != id) return BadRequest(new { message = "ID mismatch between URL and body." });
        var result = await _productService.UpdateAsync(dto, GetUserId());
        return Ok(result);
    }

    /// <summary>Soft-delete product (marks IsActive=false). Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        await _productService.SoftDeleteAsync(id, GetUserId());
        return Ok(new { message = "Product deactivated." });
    }

    /// <summary>Generate QR code PNG as Base64 for a product. For printing / display.</summary>
    [HttpGet("{id:int}/qrcode")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetQrCode(int id)
    {
        var base64 = await _productService.GenerateQrCodeAsync(id);
        return Ok(new { base64, mimeType = "image/png" });
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
}


// ═════════════════════════════════════════════════════════════════════════════
// INVENTORY CONTROLLER
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Stock movement management: GRN (goods in), adjustments, valuation, alerts.
/// All write operations restricted to Admin/Staff.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IPriceService _priceService;

    public InventoryController(IInventoryService inventoryService, IPriceService priceService)
    {
        _inventoryService = inventoryService;
        _priceService     = priceService;
    }

    /// <summary>
    /// Get all inventory transactions with optional date/type filters.
    /// </summary>
    [HttpGet("transactions")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(List<InventoryTransactionResponseDto>), 200)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? type = null)
        => Ok(await _inventoryService.GetTransactionsAsync(page, pageSize, from, to, type));

    /// <summary>
    /// Record a goods receipt (stock-in from supplier).
    /// Automatically creates an InventoryTransaction and updates Product.StockQuantity.
    /// </summary>
    [HttpPost("stock-in")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(InventoryTransactionResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RecordStockIn([FromBody] StockInDto dto)
    {
        var result = await _inventoryService.RecordStockInAsync(dto, GetUserId());
        return StatusCode(201, result);
    }

    /// <summary>
    /// Manual stock adjustment (damage, count correction, etc.).
    /// Requires a mandatory reason code and notes. Admin approval recommended.
    /// </summary>
    [HttpPost("stock-adjustment")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(InventoryTransactionResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RecordAdjustment([FromBody] StockAdjustmentDto dto)
        => Ok(await _inventoryService.RecordAdjustmentAsync(dto, GetUserId()));

    /// <summary>
    /// Current inventory valuation at live gold/silver prices.
    /// Breakdown by category (Gold, Silver, Diamond, etc.).
    /// </summary>
    [HttpGet("valuation")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(InventoryValuationDto), 200)]
    public async Task<IActionResult> GetValuation()
        => Ok(await _inventoryService.GetValuationAsync());

    /// <summary>
    /// Stock levels for all active products, sorted with low-stock items first.
    /// </summary>
    [HttpGet("stock-levels")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(List<StockLevelDto>), 200)]
    public async Task<IActionResult> GetStockLevels()
        => Ok(await _inventoryService.GetStockLevelsAsync());

    // ── Price Endpoints ───────────────────────────────────────────────────

    /// <summary>
    /// Current live gold and silver rates. Public endpoint — shown on home page ticker.
    /// Returns cached value (refreshed every 15 min) with fallback to last known price.
    /// </summary>
    [HttpGet("/api/prices/current")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GoldPriceResponseDto), 200)]
    public async Task<IActionResult> GetCurrentPrices()
    {
        var price = await _priceService.GetCurrentRateAsync();
        return Ok(new GoldPriceResponseDto(
            price.Rate22KPer10g, price.Rate24KPer10g, price.SilverRatePerKg,
            Math.Round(price.Rate22KPer10g / 10, 2),
            Math.Round(price.Rate24KPer10g / 10, 2),
            Math.Round(price.SilverRatePerKg / 1000, 2),
            price.RecordedAt, price.IsManualOverride,
            price.IsManualOverride ? "Manual" : "GoldAPI.io"));
    }

    /// <summary>
    /// Set a manual price override for today's trading session.
    /// Overrides the API price until end of the business day.
    /// </summary>
    [HttpPost("/api/prices/manual-override")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> SetManualOverride([FromBody] ManualPriceOverrideDto dto)
    {
        await _priceService.SetManualOverrideAsync(dto, GetUserId());
        return Ok(new { message = "Manual price override set for today." });
    }

    /// <summary>
    /// Historical gold/silver price records for a date range.
    /// Used in reports and for auditing loan valuations.
    /// </summary>
    [HttpGet("/api/prices/history")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(List<GoldPriceResponseDto>), 200)]
    public async Task<IActionResult> GetPriceHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
        => Ok(await _priceService.GetPriceHistoryAsync(from, to));

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
}


// ═════════════════════════════════════════════════════════════════════════════
// SALES ORDER CONTROLLER
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Point-of-sale billing: create GST invoices, process returns, download PDFs.
/// Customers can view their own orders only.
/// </summary>
[ApiController]
[Route("api/sales")]
[Authorize]
[Produces("application/json")]
public class SalesOrderController : ControllerBase
{
    private readonly ISalesOrderService _salesService;

    public SalesOrderController(ISalesOrderService salesService)
        => _salesService = salesService;

    /// <summary>
    /// Create a new sales order and generate a GST-compliant PDF invoice.
    /// Validates stock availability, applies GST (CGST+SGST or IGST for inter-state),
    /// deducts stock, and triggers invoice generation.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(SalesOrderResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateSalesOrderDto dto)
    {
        var result = await _salesService.CreateOrderAsync(dto, GetUserId());
        return CreatedAtAction(nameof(GetOrder), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing sales order (payments, status, notes, line items).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(SalesOrderResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateSalesOrderDto dto)
    {
        if (dto.Id != id) return BadRequest(new { message = "Order ID mismatch." });
        var result = await _salesService.UpdateOrderAsync(id, dto, GetUserId());
        return Ok(result);
    }

    /// <summary>
    /// Get full sales order details including all line items and GST breakdown.
    /// Customers can only access their own orders.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SalesOrderResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _salesService.GetOrderAsync(id);

        // Resource-based guard: customers see only their own orders
        var userId = GetUserId();
        if (User.IsInRole("Customer") && order.CustomerUserId != userId)
            return Forbid();

        return Ok(order);
    }

    /// <summary>
    /// Paginated sales list with filters for date range, customer, status, and payment status.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetOrders([FromQuery] SalesFilterDto filter)
    {
        var (items, total) = await _salesService.GetOrdersAsync(filter);
        return Ok(new { total, page = filter.Page, pageSize = filter.PageSize, data = items });
    }

    /// <summary>
    /// My orders — for logged-in customers to view purchase history.
    /// </summary>
    [HttpGet("my-orders")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var filter = new SalesFilterDto(
            FromDate: null, ToDate: null,
            CustomerSearch: null, Status: null, PaymentStatus: null,
            Page: page, PageSize: pageSize);
        // The service should filter by current user — extend SalesFilterDto with CustomerId
        var (items, total) = await _salesService.GetOrdersAsync(filter with
        {
            CustomerSearch = GetUserId()
        });
        return Ok(new { total, data = items });
    }

    /// <summary>
    /// Process a return for specific line items from an existing order.
    /// Restores stock and updates order status.
    /// </summary>
    [HttpPost("{id:int}/return")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(SalesOrderResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ProcessReturn(int id, [FromBody] ReturnOrderDto dto)
    {
        if (dto.OriginalOrderId != id) return BadRequest(new { message = "Order ID mismatch." });
        return Ok(await _salesService.ProcessReturnAsync(dto, GetUserId()));
    }

    /// <summary>
    /// Get (or regenerate) the PDF invoice for an order. Returns the file path/URL.
    /// </summary>
    [HttpGet("{id:int}/invoice")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetInvoice(int id)
    {
        var order = await _salesService.GetOrderAsync(id);
        if (User.IsInRole("Customer") && order.CustomerUserId != GetUserId()) return Forbid();
        var url = await _salesService.GetInvoiceUrlAsync(id);
        return Ok(new { invoiceUrl = url });
    }

    /// <summary>
    /// Send invoice to customer via email and/or SMS/WhatsApp.
    /// </summary>
    [HttpPost("{id:int}/send-invoice")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> SendInvoice(
        int id,
        [FromQuery] string? email = null,
        [FromQuery] string? phone = null)
    {
        await _salesService.SendInvoiceAsync(id, email, phone);
        return Ok(new { message = "Invoice sent." });
    }

    /// <summary>
    /// Daily sales summary with totals by payment mode and category.
    /// Used on the Admin dashboard.
    /// </summary>
    [HttpGet("daily-summary")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(DailySalesSummaryDto), 200)]
    public async Task<IActionResult> GetDailySummary([FromQuery] DateOnly? date = null)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _salesService.GetDailySummaryAsync(d));
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
}

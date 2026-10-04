using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Repositories;

namespace ShreeJewellers.Infrastructure.Services;

public interface ISalesOrderService
{
    Task<SalesOrderResponseDto> CreateOrderAsync(CreateSalesOrderDto dto, string userId);
    Task<SalesOrderResponseDto> UpdateOrderAsync(int id, UpdateSalesOrderDto dto, string userId);
    Task<SalesOrderResponseDto> GetOrderAsync(int id);
    Task<(List<SalesOrderSummaryDto> items, int total)> GetOrdersAsync(SalesFilterDto filter);
    Task<SalesOrderResponseDto> ProcessReturnAsync(ReturnOrderDto dto, string userId);
    Task<DailySalesSummaryDto> GetDailySummaryAsync(DateOnly date);
    Task<string> GetInvoiceUrlAsync(int orderId);
    Task SendInvoiceAsync(int orderId, string? email = null, string? phone = null);
}

public class SalesOrderService : ISalesOrderService
{
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _invRepo;
    private readonly IPriceService _priceService;
    private readonly IInvoiceGeneratorService _invoiceGenerator;
    private readonly INotificationService _notifications;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SalesOrderService> _logger;

    public SalesOrderService(
        IProductRepository productRepo, IInventoryRepository invRepo,
        IPriceService priceService, IInvoiceGeneratorService invoiceGenerator,
        INotificationService notifications, IMapper mapper,
        ApplicationDbContext db, ILogger<SalesOrderService> logger)
    {
        _productRepo = productRepo; _invRepo = invRepo;
        _priceService = priceService; _invoiceGenerator = invoiceGenerator;
        _notifications = notifications; _mapper = mapper;
        _db = db; _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════════
    // CREATE ORDER
    // ═════════════════════════════════════════════════════════════════════

    public async Task<SalesOrderResponseDto> CreateOrderAsync(CreateSalesOrderDto dto, string userId)
    {
        // ── Validate items and fetch products ─────────────────────────────
        var products = new Dictionary<int, Product>();
        foreach (var item in dto.Items)
        {
            if (products.ContainsKey(item.ProductId)) continue;
            var product = await _productRepo.GetByIdAsync(item.ProductId)
                ?? throw new KeyNotFoundException($"Product {item.ProductId} not found.");
            if (!product.IsActive)
                throw new InvalidOperationException($"Product '{product.Name}' is not available for sale.");
            products[item.ProductId] = product;
        }

        // ── Stock availability check ──────────────────────────────────────
        var quantityByProduct = dto.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        foreach (var (productId, qty) in quantityByProduct)
        {
            var p = products[productId];
            if (p.StockQuantity < qty)
                throw new InvalidOperationException(
                    $"Insufficient stock for '{p.Name}'. Available: {p.StockQuantity}, Requested: {qty}.");
        }

        // ── Generate Order Number ─────────────────────────────────────────
        var now         = dto.OrderDate;
        var prefix      = $"INV-{now.Year}-{now.Month:D2}-";
        var count       = await _db.SalesOrders.CountAsync(o => o.OrderNumber.StartsWith(prefix)) + 1;
        var orderNumber = $"{prefix}{count:D4}";
        while (await _db.SalesOrders.AnyAsync(o => o.OrderNumber == orderNumber))
        { count++; orderNumber = $"{prefix}{count:D4}"; }

        // ── Build Line Items ──────────────────────────────────────────────
        var orderItems   = new List<SalesOrderItem>();
        decimal gross    = 0;
        decimal totalTax = 0, cgst = 0, sgst = 0, igst = 0;

        foreach (var lineDto in dto.Items)
        {
            var p = products[lineDto.ProductId];
            var metalValue   = lineDto.WeightGrams * lineDto.RatePerGram;
            var wastageAmt   = metalValue * (p.WastagePercent / 100m);
            var subtotal     = metalValue + wastageAmt + lineDto.MakingCharges +
                               lineDto.HallmarkCharges + lineDto.StoneValue - lineDto.DiscountAmount;
            subtotal         = Math.Max(0, subtotal);

            var taxPct  = p.GSTRatePercent;
            var taxAmt  = Math.Round(subtotal * (taxPct / 100m), 2);
            var line    = Math.Round(subtotal + taxAmt, 2);

            if (dto.IsInterState)
                igst  += taxAmt;
            else
            { cgst += taxAmt / 2; sgst += taxAmt / 2; }

            totalTax += taxAmt;
            gross    += Math.Round(subtotal, 2);

            orderItems.Add(new SalesOrderItem
            {
                ProductId       = lineDto.ProductId,
                Quantity        = lineDto.Quantity,
                WeightGrams     = lineDto.WeightGrams,
                RatePerGram     = lineDto.RatePerGram,
                MakingCharges   = lineDto.MakingCharges,
                HallmarkCharges = lineDto.HallmarkCharges,
                StoneValue      = lineDto.StoneValue,
                TaxPercent      = taxPct,
                TaxAmount       = taxAmt,
                DiscountAmount  = lineDto.DiscountAmount,
                LineTotal       = line
            });
        }

        // ── Build Order ───────────────────────────────────────────────────
        var paymentMode = Enum.TryParse<PaymentMode>(dto.PaymentMode, out var pm)
            ? pm : PaymentMode.Cash;

        var netAmount = Math.Round(gross + totalTax - dto.DiscountAmount - dto.OldGoldExchangeValue, 2);
        netAmount     = Math.Max(0, netAmount);

        var paymentStatus = dto.AmountPaid >= netAmount ? PaymentStatus.Paid
            : dto.AmountPaid > 0 ? PaymentStatus.PartiallyPaid
            : PaymentStatus.Pending;

        var walkInNote = string.IsNullOrEmpty(dto.CustomerUserId)
            ? $"Walk-in: {dto.WalkInCustomerName} | {dto.WalkInCustomerPhone}"
            : null;

        var order = new SalesOrder
        {
            CustomerUserId       = dto.CustomerUserId,
            CreatedByUserId      = userId,
            OrderNumber          = orderNumber,
            OrderDate            = dto.OrderDate.ToDateTime(TimeOnly.MinValue),
            GrossAmount          = gross,
            DiscountAmount       = dto.DiscountAmount,
            TaxAmount            = Math.Round(totalTax, 2),
            OldGoldExchangeValue = dto.OldGoldExchangeValue,
            NetAmount            = netAmount,
            AmountPaid           = dto.AmountPaid,
            AdvanceAmount        = dto.AdvanceAmount,
            CGSTAmount           = Math.Round(cgst, 2),
            SGSTAmount           = Math.Round(sgst, 2),
            IGSTAmount           = Math.Round(igst, 2),
            PaymentMode          = paymentMode,
            PaymentStatus        = paymentStatus,
            Status               = OrderStatus.Confirmed,
            Notes                = walkInNote ?? dto.Notes,
            CreatedAt            = DateTime.UtcNow
        };

        await _db.SalesOrders.AddAsync(order);
        await _db.SaveChangesAsync();  // Get order.Id

        // Attach items
        foreach (var item in orderItems) item.SalesOrderId = order.Id;
        _db.SalesOrderItems.AddRange(orderItems);

        // ── Deduct Stock ──────────────────────────────────────────────────
        foreach (var (productId, qty) in quantityByProduct)
        {
            var p     = products[productId];
            var items = dto.Items.Where(i => i.ProductId == productId).ToList();
            var wt    = items.Sum(i => i.WeightGrams);

            p.StockQuantity -= qty;
            p.UpdatedAt     = DateTime.UtcNow;
            await _productRepo.UpdateAsync(p);

            await _invRepo.AddAsync(new InventoryTransaction
            {
                ProductId       = productId,
                TransactionType = TransactionType.Sale,
                Quantity        = -qty,
                WeightGrams     = -wt,
                RatePerGram     = items.First().RatePerGram,
                TotalValue      = items.Sum(i => i.WeightGrams * i.RatePerGram),
                ReferenceNo     = orderNumber,
                Notes           = $"Sale order {orderNumber}",
                CreatedByUserId = userId,
                CreatedAt       = DateTime.UtcNow
            });

            // Low-stock alert
            if (p.StockQuantity <= p.ReorderLevel)
                await _notifications.NotifyAdminsAsync($"Low Stock: {p.Name}",
                    $"{p.SKUCode} has {p.StockQuantity} left (reorder: {p.ReorderLevel}).");
        }

        await _db.SaveChangesAsync();

        // ── Generate PDF Invoice ──────────────────────────────────────────
        try
        {
            var fullOrder = await GetFullOrderAsync(order.Id);
            var invoiceUrl = await _invoiceGenerator.GenerateAsync(fullOrder);
            order.InvoiceUrl = invoiceUrl;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice generation failed for {OrderNumber}", orderNumber);
            // Don't fail the sale if invoice generation fails
        }

        // ── Audit & Notify ────────────────────────────────────────────────
        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = userId,
            Action     = "SALE_CREATE",
            EntityName = "SalesOrder",
            EntityId   = order.Id.ToString(),
            NewValues  = $"{{\"OrderNumber\":\"{orderNumber}\",\"Net\":{netAmount}}}",
            Timestamp  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(dto.CustomerUserId))
        {
            var customer = await _db.Users.FindAsync(dto.CustomerUserId);
            if (customer?.Email is not null)
                await _notifications.SendEmailAsync(customer.Email,
                    $"Invoice {orderNumber} — Shree Jewellers",
                    $"Dear {customer.FirstName},\n\nThank you for your purchase.\n" +
                    $"Invoice No: {orderNumber}\nAmount: ₹{netAmount:N2}\n\nShree Jewellers");
        }

        _logger.LogInformation("Sale {OrderNumber} created. Net: ₹{Net}", orderNumber, netAmount);
        return _mapper.Map<SalesOrderResponseDto>(await GetFullOrderAsync(order.Id));
    }

    // ═════════════════════════════════════════════════════════════════════
    // RETURNS
    // ═════════════════════════════════════════════════════════════════════

    public async Task<SalesOrderResponseDto> ProcessReturnAsync(ReturnOrderDto dto, string userId)
    {
        var original = await GetFullOrderAsync(dto.OriginalOrderId);
        if (original.Status == OrderStatus.Cancelled || original.Status == OrderStatus.Returned)
            throw new InvalidOperationException($"Cannot return a {original.Status} order.");

        var itemsToReturn = original.Items
            .Where(i => dto.ItemIdsToReturn.Contains(i.Id))
            .ToList();
        if (!itemsToReturn.Any())
            throw new InvalidOperationException("No valid items found for return.");

        // Restore stock
        foreach (var item in itemsToReturn)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId);
            if (product is null) continue;
            product.StockQuantity += item.Quantity;
            product.UpdatedAt     = DateTime.UtcNow;
            await _productRepo.UpdateAsync(product);
            await _invRepo.AddAsync(new InventoryTransaction
            {
                ProductId       = item.ProductId,
                TransactionType = TransactionType.Return,
                Quantity        = item.Quantity,
                WeightGrams     = item.WeightGrams,
                RatePerGram     = item.RatePerGram,
                TotalValue      = item.LineTotal,
                ReferenceNo     = original.OrderNumber,
                Notes           = $"Return: {dto.ReturnReason}",
                CreatedByUserId = userId,
                CreatedAt       = DateTime.UtcNow
            });
        }

        // Update order status
        var orderEntity = await _db.SalesOrders.FindAsync(dto.OriginalOrderId)!;
        var allReturned = itemsToReturn.Count == original.Items.Count;
        orderEntity!.Status    = allReturned ? OrderStatus.Returned : OrderStatus.Confirmed;
        orderEntity.UpdatedAt  = DateTime.UtcNow;
        orderEntity.Notes      = (orderEntity.Notes ?? "") + $" | RETURN: {dto.ReturnReason}";

        await _db.SaveChangesAsync();
        _logger.LogInformation("Return processed for {OrderNumber}, items: {Count}",
            original.OrderNumber, itemsToReturn.Count);

        return _mapper.Map<SalesOrderResponseDto>(await GetFullOrderAsync(dto.OriginalOrderId));
    }

    // ── Query Methods ─────────────────────────────────────────────────────

    public async Task<SalesOrderResponseDto> GetOrderAsync(int id)
        => _mapper.Map<SalesOrderResponseDto>(await GetFullOrderAsync(id));

    public async Task<(List<SalesOrderSummaryDto> items, int total)> GetOrdersAsync(SalesFilterDto filter)
    {
        var query = _db.SalesOrders.Include(o => o.Customer).AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(o => o.OrderDate >= filter.FromDate.Value.ToDateTime(TimeOnly.MinValue));
        if (filter.ToDate.HasValue)
            query = query.Where(o => o.OrderDate <= filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue));
        if (!string.IsNullOrEmpty(filter.CustomerSearch))
        {
            var s = filter.CustomerSearch.ToLower();
            query = query.Where(o =>
                o.CustomerUserId == filter.CustomerSearch ||
                (o.Customer != null && (o.Customer.FirstName.ToLower().Contains(s) ||
                 o.Customer.LastName.ToLower().Contains(s))) ||
                o.OrderNumber.ToLower().Contains(s));
        }
        if (!string.IsNullOrEmpty(filter.Status) && Enum.TryParse<OrderStatus>(filter.Status, out var st))
            query = query.Where(o => o.Status == st);
        if (!string.IsNullOrEmpty(filter.PaymentStatus) && Enum.TryParse<PaymentStatus>(filter.PaymentStatus, out var ps))
            query = query.Where(o => o.PaymentStatus == ps);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (_mapper.Map<List<SalesOrderSummaryDto>>(items), total);
    }

    public async Task<DailySalesSummaryDto> GetDailySummaryAsync(DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end   = date.ToDateTime(TimeOnly.MaxValue);

        var orders = await _db.SalesOrders
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .Where(o => o.OrderDate >= start && o.OrderDate <= end &&
                        o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        return new DailySalesSummaryDto(
            date,
            orders.Count,
            orders.Sum(o => o.GrossAmount),
            orders.Sum(o => o.DiscountAmount),
            orders.Sum(o => o.TaxAmount),
            orders.Sum(o => o.NetAmount),
            orders.Sum(o => o.AmountPaid),
            orders.GroupBy(o => o.PaymentMode.ToString())
                  .ToDictionary(g => g.Key, g => g.Sum(o => o.NetAmount)),
            orders.SelectMany(o => o.Items)
                  .GroupBy(i => i.Product?.Category?.Name ?? "Unknown")
                  .ToDictionary(g => g.Key, g => g.Sum(i => i.LineTotal)));
    }

    public async Task<string> GetInvoiceUrlAsync(int orderId)
    {
        var order = await _db.SalesOrders.FindAsync(orderId)
            ?? throw new KeyNotFoundException();
        if (string.IsNullOrEmpty(order.InvoiceUrl))
        {
            var full = await GetFullOrderAsync(orderId);
            order.InvoiceUrl = await _invoiceGenerator.GenerateAsync(full);
            await _db.SaveChangesAsync();
        }
        return order.InvoiceUrl!;
    }

    public async Task SendInvoiceAsync(int orderId, string? email = null, string? phone = null)
    {
        var order   = await GetOrderAsync(orderId);
        var subject = $"Invoice {order.OrderNumber} — Shree Jewellers";
        var body    = $"Please find your invoice attached.\nOrder: {order.OrderNumber}\nAmount: ₹{order.NetAmount:N2}";

        if (email is not null)
            await _notifications.SendEmailAsync(email, subject, body);
        if (phone is not null)
            await _notifications.SendSmsAsync(phone, $"Shree Jewellers Invoice {order.OrderNumber}: Rs.{order.NetAmount:N0}");
    }

    public async Task<SalesOrderResponseDto> UpdateOrderAsync(int id, UpdateSalesOrderDto dto, string userId)
    {
        var order = await GetFullOrderAsync(id);

        if (!string.IsNullOrWhiteSpace(dto.CustomerUserId))
        {
            order.CustomerUserId = dto.CustomerUserId;
        }

        order.OrderDate = dto.OrderDate.ToDateTime(TimeOnly.MinValue);

        if (Enum.TryParse<PaymentMode>(dto.PaymentMode, true, out var pm))
            order.PaymentMode = pm;

        order.Notes = dto.Notes;
        order.DiscountAmount = Math.Max(0, dto.DiscountAmount);
        order.AdvanceAmount = Math.Max(0, dto.AdvanceAmount);
        order.OldGoldExchangeValue = Math.Max(0, dto.OldGoldExchangeValue);

        if (dto.Items != null && dto.Items.Count > 0)
        {
            _db.SalesOrderItems.RemoveRange(order.Items);
            order.Items.Clear();

            decimal gross = 0;
            decimal totalTax = 0;

            foreach (var lineDto in dto.Items)
            {
                var p = await _productRepo.GetByIdAsync(lineDto.ProductId)
                    ?? throw new KeyNotFoundException($"Product {lineDto.ProductId} not found.");

                var metalValue = lineDto.WeightGrams * lineDto.RatePerGram;
                var wastageAmt = metalValue * (p.WastagePercent / 100m);
                var subtotal = metalValue + wastageAmt + lineDto.MakingCharges +
                               lineDto.HallmarkCharges + lineDto.StoneValue - lineDto.DiscountAmount;
                subtotal = Math.Max(0, subtotal);

                var taxPct = p.GSTRatePercent;
                var taxAmt = Math.Round(subtotal * (taxPct / 100m), 2);

                var item = new SalesOrderItem
                {
                    SalesOrderId = order.Id,
                    ProductId = lineDto.ProductId,
                    Quantity = lineDto.Quantity,
                    WeightGrams = lineDto.WeightGrams,
                    RatePerGram = lineDto.RatePerGram,
                    MakingCharges = lineDto.MakingCharges,
                    HallmarkCharges = lineDto.HallmarkCharges,
                    StoneValue = lineDto.StoneValue,
                    DiscountAmount = lineDto.DiscountAmount,
                    TaxPercent = taxPct,
                    TaxAmount = taxAmt,
                    LineTotal = Math.Round(subtotal + taxAmt, 2)
                };
                order.Items.Add(item);
                gross += subtotal;
                totalTax += taxAmt;
            }

            order.GrossAmount = gross;
            order.TaxAmount = totalTax;

            if (dto.IsInterState)
            {
                order.IGSTAmount = totalTax;
                order.CGSTAmount = 0;
                order.SGSTAmount = 0;
            }
            else
            {
                order.CGSTAmount = Math.Round(totalTax / 2m, 2);
                order.SGSTAmount = totalTax - order.CGSTAmount;
                order.IGSTAmount = 0;
            }

            order.NetAmount = Math.Max(0, gross + totalTax - order.DiscountAmount - order.OldGoldExchangeValue);
        }

        order.AmountPaid = Math.Max(0, dto.AmountPaid);

        if (order.AmountPaid >= order.NetAmount && order.NetAmount > 0)
        {
            order.PaymentStatus = PaymentStatus.Paid;
        }
        else if (order.AmountPaid > 0)
        {
            order.PaymentStatus = PaymentStatus.PartiallyPaid;
        }
        else if (Enum.TryParse<PaymentStatus>(dto.PaymentStatus, true, out var ps))
        {
            order.PaymentStatus = ps;
        }

        if (Enum.TryParse<OrderStatus>(dto.Status, true, out var os))
        {
            order.Status = os;
        }

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Sales Order {OrderNumber} (ID: {Id}) updated by {UserId}", order.OrderNumber, order.Id, userId);

        return _mapper.Map<SalesOrderResponseDto>(order);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private async Task<SalesOrder> GetFullOrderAsync(int id)
        => await _db.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException($"Sales order {id} not found.");
}

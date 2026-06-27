/*
 * ============================================================
 * SHREE JEWELLERS — Value-Add Feature Stubs
 * FutureFeatures.cs
 *
 * These are fully-documented stubs ready for implementation.
 * Each includes: interface, TODO items, integration notes.
 * ============================================================
 */

namespace ShreeJewellers.Features;

// ─────────────────────────────────────────────────────────────
// 1. WHATSAPP BUSINESS API
//    Send loan reminders, repayment confirmations, KYC updates
// ─────────────────────────────────────────────────────────────

public interface IWhatsAppService
{
    Task SendLoanReminderAsync(string phoneNumber, string customerName, string loanNumber, DateOnly dueDate, decimal outstanding);
    Task SendRepaymentConfirmationAsync(string phoneNumber, string receiptNumber, decimal amountPaid, decimal balanceRemaining);
    Task SendKYCStatusUpdateAsync(string phoneNumber, string customerName, bool isVerified, string? rejectionReason = null);
    Task SendTemplateMessageAsync(string phoneNumber, string templateName, Dictionary<string, string> parameters);
}

public class WhatsAppService : IWhatsAppService
{
    // TODO: Integrate with WhatsApp Business Cloud API (Meta)
    // Docs: https://developers.facebook.com/docs/whatsapp/cloud-api
    //
    // Required:
    //   - Meta Developer Account + Business Verification
    //   - WhatsApp Business Number registration
    //   - Message templates pre-approved by Meta
    //
    // NuGet: None needed — use HttpClient to call Graph API
    // Endpoint: POST https://graph.facebook.com/v18.0/{PHONE_NUMBER_ID}/messages
    // Auth: Bearer {WHATSAPP_API_TOKEN} from appsettings
    //
    // Template example (must be approved):
    //   Name: loan_due_reminder
    //   Body: "Dear {{1}}, your gold loan {{2}} is due on {{3}}. Outstanding: ₹{{4}}."
    //
    // Config needed in appsettings.json:
    //   "WhatsApp": {
    //     "AccessToken": "ENV: WHATSAPP_ACCESS_TOKEN",
    //     "PhoneNumberId": "ENV: WHATSAPP_PHONE_NUMBER_ID",
    //     "BusinessAccountId": "ENV: WHATSAPP_BUSINESS_ACCOUNT_ID"
    //   }

    public Task SendLoanReminderAsync(string phoneNumber, string customerName,
        string loanNumber, DateOnly dueDate, decimal outstanding)
    {
        // TODO: Implement WhatsApp template message
        // Template: loan_due_reminder
        // Parameters: {{1}}=customerName, {{2}}=loanNumber, {{3}}=dueDate, {{4}}=outstanding
        throw new NotImplementedException("WhatsApp integration pending. See FutureFeatures.cs TODO.");
    }

    public Task SendRepaymentConfirmationAsync(string phoneNumber, string receiptNumber,
        decimal amountPaid, decimal balanceRemaining)
    {
        // TODO: Implement WhatsApp template message
        // Template: repayment_confirmation
        throw new NotImplementedException();
    }

    public Task SendKYCStatusUpdateAsync(string phoneNumber, string customerName,
        bool isVerified, string? rejectionReason = null)
    {
        throw new NotImplementedException();
    }

    public Task SendTemplateMessageAsync(string phoneNumber, string templateName,
        Dictionary<string, string> parameters)
    {
        throw new NotImplementedException();
    }
}

// ─────────────────────────────────────────────────────────────
// 2. SMS GATEWAY (MSG91 / Textlocal)
//    OTP delivery, payment confirmations
// ─────────────────────────────────────────────────────────────

public interface ISMSGatewayService
{
    Task<bool> SendOTPAsync(string phoneNumber, string otp, string purpose);
    Task SendTransactionalSMSAsync(string phoneNumber, string message);
    Task<string> GenerateOTPAsync(string userId, string purpose);
    Task<bool> VerifyOTPAsync(string userId, string otp, string purpose);
}

public class MSG91SMSService : ISMSGatewayService
{
    // TODO: Implement MSG91 integration
    // Docs: https://docs.msg91.com/
    // NuGet: RestSharp or HttpClient
    //
    // Config needed:
    //   "SMS": {
    //     "Provider": "MSG91",
    //     "AuthKey": "ENV: MSG91_AUTH_KEY",
    //     "SenderId": "SRJWL",
    //     "OTPTemplateId": "ENV: MSG91_OTP_TEMPLATE_ID",
    //     "TransactionalTemplateId": "ENV: MSG91_TRANS_TEMPLATE_ID"
    //   }
    //
    // OTP Flow:
    //   1. GenerateOTPAsync() → generates 6-digit OTP, stores hashed in Redis/MemCache with 10-min TTL
    //   2. Calls MSG91 Send OTP API
    //   3. VerifyOTPAsync() → compares hash, marks as used (one-time use)

    public Task<bool> SendOTPAsync(string phoneNumber, string otp, string purpose)
        => throw new NotImplementedException("MSG91 OTP pending. See FutureFeatures.cs.");

    public Task SendTransactionalSMSAsync(string phoneNumber, string message)
        => throw new NotImplementedException();

    public Task<string> GenerateOTPAsync(string userId, string purpose)
        => throw new NotImplementedException();

    public Task<bool> VerifyOTPAsync(string userId, string otp, string purpose)
        => throw new NotImplementedException();
}

// ─────────────────────────────────────────────────────────────
// 3. BARCODE SCANNER SUPPORT (billing page)
// ─────────────────────────────────────────────────────────────

/*
 * IMPLEMENTATION PLAN:
 *
 * A. USB Barcode Scanner (keyboard wedge — zero code needed):
 *    - USB scanners emulate keyboard input
 *    - billing.js listens for rapid key input (< 50ms between chars)
 *    - Detects "SJ|productId|SKU|purity" pattern
 *    - Auto-fills product search field
 *
 * B. Camera Barcode Scanner (ZXing.js in browser):
 *    - Add to billing page: <script src="https://unpkg.com/@zxing/library@latest"></script>
 *    - Open camera modal, scan QR code
 *    - Parse "SJ|productId|SKU|purity" and call /api/products/by-sku/{sku}
 *
 * billing.js additions:
 *
 * const BarcodeScanner = {
 *     buffer: '', lastKey: 0,
 *     init() {
 *         document.addEventListener('keypress', this.handleKey.bind(this));
 *     },
 *     handleKey(e) {
 *         const now = Date.now();
 *         if (now - this.lastKey < 50) this.buffer += e.key;
 *         else this.buffer = e.key;
 *         this.lastKey = now;
 *         if (e.key === 'Enter' && this.buffer.startsWith('SJ|')) {
 *             const parts = this.buffer.split('|');
 *             this.onScanned(parts[1], parts[2]); // productId, SKU
 *         }
 *     },
 *     async onScanned(productId, sku) {
 *         const product = await API.get('/products/by-sku/' + sku);
 *         BillingPage.addItem(product);
 *         Toast.success('Product scanned: ' + product.name);
 *     }
 * };
 */

// ─────────────────────────────────────────────────────────────
// 4. PROGRESSIVE WEB APP (PWA)
// ─────────────────────────────────────────────────────────────

/*
 * Files to create:
 *
 * A. /wwwroot/manifest.json
 * {
 *   "name": "Shree Jewellers",
 *   "short_name": "ShreeJwl",
 *   "start_url": "/portal/dashboard",
 *   "display": "standalone",
 *   "background_color": "#1a1a2e",
 *   "theme_color": "#c9a84c",
 *   "icons": [
 *     {"src": "/img/icon-192.png", "sizes": "192x192", "type": "image/png"},
 *     {"src": "/img/icon-512.png", "sizes": "512x512", "type": "image/png"}
 *   ]
 * }
 *
 * B. /wwwroot/sw.js (Service Worker)
 *    - Caches: dashboard HTML, CSS, site.js, loan-calculator.js
 *    - Offline fallback: shows cached KPI values when network unavailable
 *    - Background sync: queue repayment recordings when offline, sync when online
 *    - Push notifications: loan reminders (requires Web Push API + VAPID keys)
 *
 * C. Add to _Layout.cshtml:
 *    <link rel="manifest" href="/manifest.json">
 *    <script>if('serviceWorker' in navigator) navigator.serviceWorker.register('/sw.js');</script>
 */

// ─────────────────────────────────────────────────────────────
// 5. CUSTOMER LOYALTY POINTS
// ─────────────────────────────────────────────────────────────

public interface ILoyaltyService
{
    Task AwardPointsForPurchaseAsync(string customerId, decimal purchaseAmount);
    Task<bool> RedeemPointsAsync(string customerId, int pointsToRedeem, int salesOrderId);
    Task<int> GetBalanceAsync(string customerId);
    Task<decimal> GetRedemptionValueAsync(int points);
    Task<List<LoyaltyTransaction>> GetHistoryAsync(string customerId);
}

public record LoyaltyTransaction(
    string Type,       // "Earned" | "Redeemed" | "Expired"
    int Points,
    string Description,
    DateTime Timestamp,
    string? OrderNumber);

public class LoyaltyService : ILoyaltyService
{
    // TODO: Implement loyalty points system
    //
    // Rules (configurable from AppSettings):
    //   - Earn 1 point per ₹100 spent on jewellery purchases (LoyaltyPointsPerRupee = 0.01)
    //   - 1 point = ₹0.50 redemption value
    //   - Maximum 10% discount via points on any single invoice
    //   - Points expire after 2 years of inactivity
    //   - Points NOT earned on gold loan repayments (cash transactions, not purchases)
    //
    // DB changes needed:
    //   - ApplicationUser.LoyaltyPoints already exists ✓
    //   - New table: LoyaltyTransactions (Id, UserId, Type, Points, Description, OrderId, Timestamp)
    //
    // Integration with SalesOrderService:
    //   - After order confirmed: await _loyaltyService.AwardPointsForPurchaseAsync(customerId, netAmount)
    //   - Before billing: if (dto.RedeemLoyaltyPoints) discount = await _loyaltyService.RedeemPointsAsync(...)

    public Task AwardPointsForPurchaseAsync(string customerId, decimal purchaseAmount)
        => throw new NotImplementedException("Loyalty points system pending. See FutureFeatures.cs.");

    public Task<bool> RedeemPointsAsync(string customerId, int pointsToRedeem, int salesOrderId)
        => throw new NotImplementedException();

    public Task<int> GetBalanceAsync(string customerId) => throw new NotImplementedException();
    public Task<decimal> GetRedemptionValueAsync(int points) => throw new NotImplementedException();
    public Task<List<LoyaltyTransaction>> GetHistoryAsync(string customerId) => throw new NotImplementedException();
}

// ─────────────────────────────────────────────────────────────
// 6. FESTIVAL OFFER MANAGEMENT
// ─────────────────────────────────────────────────────────────

public record FestivalOffer(
    int Id,
    string OfferName,        // "Diwali 2024 — Free Making Charges"
    string Description,
    OfferType Type,          // MakingChargeDiscount | FixedDiscount | PercentDiscount | FreeHallmark
    decimal DiscountValue,   // % or flat ₹
    DateTime ValidFrom,
    DateTime ValidTo,
    int? CategoryId,         // null = all categories
    decimal? MinPurchaseAmount,
    int MaxUsesPerCustomer,
    bool IsActive
);

public enum OfferType { MakingChargeDiscount, PercentDiscount, FixedAmount, FreeHallmark }

// TODO: Implement OfferService, OffersController, Admin UI for offer CRUD
// Apply discounts in SalesOrderService before billing calculation

// ─────────────────────────────────────────────────────────────
// 7. PUBLIC OLD GOLD VALUATION CALCULATOR
// ─────────────────────────────────────────────────────────────

/*
 * Public page: /tools/gold-valuation
 * No login required — customer acquisition tool
 *
 * Input:
 *   - Weight in grams
 *   - Purity (22K / 24K / 18K)
 *   - Stone weight (optional, to deduct)
 *
 * Output (calculated client-side using live price from /api/prices/current):
 *   - Net gold weight = Gross - Stone weight
 *   - Current market value = Net weight × rate/g
 *   - Estimated loan eligibility = Market value × 75% LTV
 *   - Melting value = Market value × (1 - wastage%)
 *
 * JS implementation in loan-calculator.js:
 *   function calcOldGoldValue(grossGrams, stoneGrams, purity, ratePerGram) {
 *     const netGrams = grossGrams - (stoneGrams || 0);
 *     const purityFactor = purity === '24K' ? 1 : purity === '22K' ? (22/24) : purity === '18K' ? (18/24) : 0.916;
 *     const value = netGrams * purityFactor * ratePerGram;
 *     return { netGrams, value, loanEligible: value * 0.75 };
 *   }
 */

// ─────────────────────────────────────────────────────────────
// 8. BANK ACCOUNT INTEGRATION (Digital Loan Disbursement)
// ─────────────────────────────────────────────────────────────

public interface IBankTransferService
{
    Task<string> InitiateLoanDisbursementAsync(string customerId, decimal amount,
        string bankAccount, string ifscCode, string beneficiaryName);
    Task<DisbursementStatus> GetDisbursementStatusAsync(string referenceId);
    Task<bool> VerifyBankAccountAsync(string accountNumber, string ifscCode);
}

public record DisbursementStatus(
    string ReferenceId,
    string Status,           // "Pending" | "Processing" | "Success" | "Failed"
    string? UTRNumber,
    DateTime? ProcessedAt,
    string? FailureReason);

// TODO: Integrate with Razorpay Payout API or NEFT/RTGS via bank API
// Docs: https://razorpay.com/docs/razorpay-x/payout-links/
//
// Flow:
//   1. Admin records gold loan creation in app
//   2. System calls InitiateLoanDisbursementAsync() to bank API
//   3. Bank processes NEFT/RTGS (2–4 hours)
//   4. Webhook received → update loan record with UTR number
//   5. Customer notified via SMS/WhatsApp with transaction details
//
// Config needed:
//   "BankAPI": {
//     "Provider": "Razorpay",  // or "HDFC" | "ICICI" | "Kotak"
//     "ApiKey": "ENV: BANK_API_KEY",
//     "ApiSecret": "ENV: BANK_API_SECRET",
//     "AccountNumber": "ENV: BANK_ACCOUNT_NUMBER"
//   }

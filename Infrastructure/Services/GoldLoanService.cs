using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShreeJewellers.Application.DTOs.GoldLoan;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Repositories;

namespace ShreeJewellers.Infrastructure.Services;

public interface IGoldLoanService
{
    Task<GoldLoanResponseDto> CreateLoanAsync(CreateGoldLoanDto dto, string createdByUserId);
    Task<LoanEligibilityResponseDto> CheckEligibilityAsync(LoanEligibilityRequestDto request);
    Task<RepaymentResponseDto> RecordRepaymentAsync(RecordRepaymentDto dto, string createdByUserId);
    Task<OutstandingBalanceDto> GetOutstandingBalanceAsync(int loanId, DateOnly? asOfDate = null);
    Task<decimal> CalculateAccruedInterestAsync(int loanId, DateOnly fromDate, DateOnly toDate);
    Task<decimal> CalculatePenaltyInterestAsync(int loanId);
    Task<RepaymentScheduleDto> GenerateRepaymentScheduleAsync(int loanId);
    Task ExtendLoanAsync(ExtendLoanDto dto, string adminUserId);
    Task CloseLoanAsync(CloseLoanDto dto, string adminUserId);
    Task ProcessAuctionAsync(AuctionGoldDto dto, string adminUserId);
    Task CheckAndMarkDefaultedLoansAsync();
    Task<GoldLoanResponseDto> GetLoanAsync(int id);
    Task<List<GoldLoanResponseDto>> GetCustomerLoansAsync(string customerId);
    Task<List<GoldLoanSummaryDto>> GetActiveLoansAsync(int page, int pageSize, string? search = null);
    Task<List<GoldLoanSummaryDto>> GetDefaultedLoansAsync();
    Task<List<GoldLoanSummaryDto>> GetLoansDueThisWeekAsync();
}

public class GoldLoanService : IGoldLoanService
{
    private readonly IGoldLoanRepository _loanRepo;
    private readonly IInterestCalculationEngine _calculator;
    private readonly IPriceService _priceService;
    private readonly INotificationService _notificationService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GoldLoanService> _logger;

    public GoldLoanService(
        IGoldLoanRepository loanRepo,
        IInterestCalculationEngine calculator,
        IPriceService priceService,
        INotificationService notificationService,
        ApplicationDbContext db,
        ILogger<GoldLoanService> logger)
    {
        _loanRepo            = loanRepo;
        _calculator          = calculator;
        _priceService        = priceService;
        _notificationService = notificationService;
        _db                  = db;
        _logger              = logger;
    }

    // ═════════════════════════════════════════════════════════════════════
    // LOAN CREATION
    // ═════════════════════════════════════════════════════════════════════

    public async Task<LoanEligibilityResponseDto> CheckEligibilityAsync(LoanEligibilityRequestDto request)
    {
        var customer = await _db.Users.FindAsync(request.CustomerUserId);
        if (customer is null)
            return new LoanEligibilityResponseDto(false, "Customer not found.",
                0, 0, 0, 0, 0, "", 0, "", DateOnly.MinValue);

        if (!customer.IsActive)
            return new LoanEligibilityResponseDto(false, "Customer account is inactive.",
                0, 0, 0, 0, 0, "", 0, "", DateOnly.MinValue);

        if (customer.KYCStatus != KYCStatus.Verified)
            return new LoanEligibilityResponseDto(false,
                $"KYC verification is required before a gold loan can be issued. Current status: {customer.KYCStatus}.",
                0, 0, 0, 0, 0, "", 0, "", DateOnly.MinValue);

        var activeLoans = await _db.GoldLoans
            .Where(l => l.CustomerUserId == request.CustomerUserId &&
                        (l.LoanStatus == LoanStatus.Active ||
                         l.LoanStatus == LoanStatus.PartiallyRepaid ||
                         l.LoanStatus == LoanStatus.Extended))
            .CountAsync();

        // Business rule: max 3 simultaneous active loans per customer
        if (activeLoans >= 3)
            return new LoanEligibilityResponseDto(false,
                "Maximum of 3 simultaneous gold loans allowed per customer.",
                0, 0, 0, 0, 0, "", 0, "", DateOnly.MinValue);

        var interestSetting = await GetActiveInterestSettingAsync();
        var goldPrice       = await _priceService.GetCurrentRateAsync();

        var totalWeight     = request.GoldItems.Sum(i => i.WeightGrams);
        var firstPurity     = request.GoldItems.FirstOrDefault()?.Purity ?? "22K";
        var rateToUse       = firstPurity.StartsWith("24") ? goldPrice.Rate24KPer10g : goldPrice.Rate22KPer10g;
        var totalGoldValue  = (totalWeight / 10m) * rateToUse;
        var maxLoan         = Math.Round(totalGoldValue * (interestSetting.LoanToValuePercent / 100m), 2);
        var maturityDate    = DateOnly.FromDateTime(DateTime.Today)
                                .AddMonths(interestSetting.DefaultTenureMonths);

        return new LoanEligibilityResponseDto(
            IsEligible:           true,
            IneligibilityReason:  null,
            TotalGoldWeightGrams: totalWeight,
            TotalGoldMarketValue: Math.Round(totalGoldValue, 2),
            LoanToValuePercent:   interestSetting.LoanToValuePercent,
            MaxEligibleLoanAmount: maxLoan,
            CurrentGoldRatePer10g: rateToUse,
            GoldPurityAssumed:    firstPurity,
            InterestRatePercent:  interestSetting.InterestRatePercent,
            InterestCalculationType: interestSetting.CalculationType.ToString(),
            ProposedMaturityDate: maturityDate);
    }

    public async Task<GoldLoanResponseDto> CreateLoanAsync(CreateGoldLoanDto dto, string createdByUserId)
    {
        // ── Validation ────────────────────────────────────────────────────

        var customer = await _db.Users.FindAsync(dto.CustomerUserId)
            ?? throw new KeyNotFoundException($"Customer {dto.CustomerUserId} not found.");

        if (customer.KYCStatus != KYCStatus.Verified)
            throw new InvalidOperationException(
                $"Cannot create gold loan: customer KYC status is {customer.KYCStatus}. KYC must be Verified.");

        if (!customer.IsActive)
            throw new InvalidOperationException("Cannot create gold loan for an inactive customer account.");

        if (!dto.GoldItems.Any())
            throw new InvalidOperationException("At least one gold item must be deposited.");

        var interestSetting = await GetActiveInterestSettingAsync();
        var goldPrice       = await _priceService.GetCurrentRateAsync();

        // ── Loan-to-Value Calculation ─────────────────────────────────────

        var totalWeight    = dto.GoldItems.Sum(i => i.WeightGrams);
        var rateToUse      = dto.GoldPurity.StartsWith("24")
                             ? goldPrice.Rate24KPer10g
                             : goldPrice.Rate22KPer10g;
        var totalGoldValue = (totalWeight / 10m) * rateToUse;
        var maxLoanAmount  = Math.Round(totalGoldValue * (interestSetting.LoanToValuePercent / 100m), 2);

        if (dto.PrincipalAmount > maxLoanAmount)
            throw new InvalidOperationException(
                $"Requested loan amount ₹{dto.PrincipalAmount:N2} exceeds maximum eligible amount " +
                $"₹{maxLoanAmount:N2} (based on {interestSetting.LoanToValuePercent}% LTV of gold value ₹{totalGoldValue:N2}).");

        if (dto.PrincipalAmount <= 0)
            throw new InvalidOperationException("Loan amount must be greater than zero.");

        // ── Generate Loan Number ──────────────────────────────────────────

        var year     = dto.LoanDate.Year;
        var sequence = await _loanRepo.GetNextSequenceForYearAsync(year);
        var loanNumber = $"GL-{year}-{sequence:D4}";

        // Ensure uniqueness (race condition guard)
        while (await _db.GoldLoans.AnyAsync(l => l.LoanNumber == loanNumber))
        {
            sequence++;
            loanNumber = $"GL-{year}-{sequence:D4}";
        }

        // ── Build Entity ──────────────────────────────────────────────────

        var loan = new GoldLoan
        {
            LoanNumber                = loanNumber,
            CustomerUserId            = dto.CustomerUserId,
            CreatedByUserId           = createdByUserId,
            InterestSettingId         = interestSetting.Id,
            LoanDate                  = dto.LoanDate,
            MaturityDate              = dto.LoanDate.AddMonths(interestSetting.DefaultTenureMonths),
            PrincipalAmount           = dto.PrincipalAmount,
            GoldDepositedWeightGrams  = totalWeight,
            GoldPurity                = dto.GoldPurity,
            GoldCurrentValueAtDeposit = totalGoldValue,
            LoanToValuePercent        = interestSetting.LoanToValuePercent,
            LoanStatus                = LoanStatus.Active,
            TotalRepaid               = 0m,
            Notes                     = dto.Notes,
            CreatedAt                 = DateTime.UtcNow
        };

        await _loanRepo.AddAsync(loan);
        await _loanRepo.SaveAsync();   // Get Id before adding items

        // ── Gold Items ────────────────────────────────────────────────────

        foreach (var item in dto.GoldItems)
        {
            var goldItem = new GoldLoanItem
            {
                GoldLoanId      = loan.Id,
                ItemDescription = item.ItemDescription,
                WeightGrams     = item.WeightGrams,
                Purity          = item.Purity,
                EstimatedValue  = item.EstimatedValue,
                HallmarkNumber  = item.HallmarkNumber
            };

            _db.GoldLoanItems.Add(goldItem);
        }

        // ── Audit Log ─────────────────────────────────────────────────────

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = createdByUserId,
            Action     = "GOLD_LOAN_CREATE",
            EntityName = "GoldLoan",
            EntityId   = loan.Id.ToString(),
            NewValues  = $"{{\"LoanNumber\":\"{loanNumber}\",\"Principal\":{dto.PrincipalAmount}," +
                         $"\"Customer\":\"{dto.CustomerUserId}\",\"GoldWeight\":{totalWeight}}}",
            Timestamp  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // ── Notifications ─────────────────────────────────────────────────

        await _notificationService.SendInAppAsync(
            dto.CustomerUserId,
            $"Gold Loan Created — {loanNumber}",
            $"Your gold loan of ₹{dto.PrincipalAmount:N2} has been created. " +
            $"Maturity date: {loan.MaturityDate:dd-MMM-yyyy}. " +
            $"Interest: {interestSetting.InterestRatePercent}% {interestSetting.CalculationType}.",
            "GoldLoan", loan.Id);

        await _notificationService.SendEmailAsync(
            customer.Email!,
            $"Gold Loan Created — {loanNumber} | Shree Jewellers",
            BuildLoanCreatedEmailBody(customer, loan, interestSetting));

        _logger.LogInformation(
            "Gold loan {LoanNumber} created for customer {CustomerId}. Amount: ₹{Amount}",
            loanNumber, dto.CustomerUserId, dto.PrincipalAmount);

        return await MapToResponseDto(loan);
    }

    // ═════════════════════════════════════════════════════════════════════
    // REPAYMENT
    // ═════════════════════════════════════════════════════════════════════

    public async Task<RepaymentResponseDto> RecordRepaymentAsync(
        RecordRepaymentDto dto, string createdByUserId)
    {
        var loan = await _loanRepo.GetByIdAsync(dto.GoldLoanId)
            ?? throw new KeyNotFoundException($"Gold loan {dto.GoldLoanId} not found.");

        if (loan.LoanStatus is LoanStatus.Closed or LoanStatus.Defaulted)
            throw new InvalidOperationException(
                $"Cannot record repayment — loan is {loan.LoanStatus}.");

        if (dto.AmountPaid <= 0)
            throw new InvalidOperationException("Repayment amount must be greater than zero.");

        // ── Calculate what's owed as of repayment date ────────────────────

        var balance = await ComputeOutstandingAsync(loan, dto.RepaymentDate);

        if (dto.AmountPaid > balance.TotalOutstanding + 1m) // 1 rupee tolerance
            throw new InvalidOperationException(
                $"Payment ₹{dto.AmountPaid:N2} exceeds outstanding balance ₹{balance.TotalOutstanding:N2}.");

        // ── Interest-first allocation ─────────────────────────────────────
        // Payment waterfall: Penalty → Interest → Principal

        var remaining      = dto.AmountPaid;
        var penaltyCleared = Math.Min(remaining, balance.PenaltyInterest);
        remaining         -= penaltyCleared;

        var interestCleared = Math.Min(remaining, balance.AccruedInterest);
        remaining          -= interestCleared;

        var principalCleared = Math.Min(remaining, balance.PrincipalRemaining);

        var principalBalanceAfter = balance.PrincipalRemaining - principalCleared;

        // ── Generate Receipt Number ───────────────────────────────────────

        var receiptCount   = await _db.LoanRepayments.CountAsync() + 1;
        var receiptNumber  = $"RCP-{DateTime.Today.Year}-{receiptCount:D4}";

        while (await _db.LoanRepayments.AnyAsync(r => r.ReceiptNumber == receiptNumber))
        {
            receiptCount++;
            receiptNumber = $"RCP-{DateTime.Today.Year}-{receiptCount:D4}";
        }

        // ── Create Repayment Record ───────────────────────────────────────

        var paymentMode = Enum.TryParse<PaymentMode>(dto.PaymentMode, out var pm)
            ? pm : PaymentMode.Cash;

        var repayment = new LoanRepayment
        {
            GoldLoanId            = loan.Id,
            CreatedByUserId       = createdByUserId,
            RepaymentDate         = dto.RepaymentDate,
            AmountPaid            = dto.AmountPaid,
            PrincipalComponent    = principalCleared,
            InterestComponent     = interestCleared + penaltyCleared,
            PenaltyAmount         = penaltyCleared,
            PaymentMode           = paymentMode,
            ReceiptNumber         = receiptNumber,
            PrincipalBalanceAfter = principalBalanceAfter,
            Notes                 = dto.Notes,
            CreatedAt             = DateTime.UtcNow
        };

        _db.LoanRepayments.Add(repayment);

        // ── Update Loan ───────────────────────────────────────────────────

        loan.TotalRepaid += dto.AmountPaid;

        bool isFullyPaid = principalBalanceAfter <= 0.01m; // 1 paisa tolerance

        if (isFullyPaid)
        {
            loan.LoanStatus  = LoanStatus.Closed;
            loan.ClosedDate  = dto.RepaymentDate;
            loan.UpdatedAt   = DateTime.UtcNow;
        }
        else if (loan.LoanStatus == LoanStatus.Active && dto.AmountPaid > 0)
        {
            loan.LoanStatus = LoanStatus.PartiallyRepaid;
            loan.UpdatedAt  = DateTime.UtcNow;
        }

        // ── Audit Log ─────────────────────────────────────────────────────

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = createdByUserId,
            Action     = "REPAYMENT_RECORD",
            EntityName = "LoanRepayment",
            EntityId   = loan.Id.ToString(),
            NewValues  = $"{{\"Receipt\":\"{receiptNumber}\",\"AmountPaid\":{dto.AmountPaid}," +
                         $"\"Principal\":{principalCleared},\"Interest\":{interestCleared}," +
                         $"\"Penalty\":{penaltyCleared},\"BalanceAfter\":{principalBalanceAfter}}}",
            Timestamp  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // ── Notifications ─────────────────────────────────────────────────

        var customer = loan.Customer;
        var message  = isFullyPaid
            ? $"Congratulations! Your gold loan {loan.LoanNumber} is fully repaid. " +
              $"Please collect your pledged gold items."
            : $"Repayment of ₹{dto.AmountPaid:N2} recorded for loan {loan.LoanNumber}. " +
              $"Remaining balance: ₹{principalBalanceAfter:N2} (principal).";

        await _notificationService.SendInAppAsync(customer.Id, isFullyPaid ? "Gold Loan Closed" : "Repayment Recorded", message, "GoldLoan", loan.Id);

        if (isFullyPaid)
        {
            await _notificationService.SendEmailAsync(
                customer.Email!,
                $"Gold Loan Closed — {loan.LoanNumber} | Shree Jewellers",
                $"Dear {customer.FirstName},\n\n" +
                $"Your gold loan {loan.LoanNumber} has been fully repaid.\n" +
                $"Please visit the shop to collect your pledged gold items.\n\n" +
                $"Receipt No: {receiptNumber}\n" +
                $"Amount Paid: ₹{dto.AmountPaid:N2}\n\n" +
                $"Thank you for choosing Shree Jewellers.");
        }

        _logger.LogInformation(
            "Repayment {Receipt} recorded for loan {LoanNumber}. Paid: ₹{Amount}. " +
            "Principal remaining: ₹{Balance}. Status: {Status}",
            receiptNumber, loan.LoanNumber, dto.AmountPaid, principalBalanceAfter, loan.LoanStatus);

        return new RepaymentResponseDto(
            repayment.Id, receiptNumber, dto.RepaymentDate,
            dto.AmountPaid, principalCleared, interestCleared, penaltyCleared,
            paymentMode.ToString(), principalBalanceAfter, dto.Notes, DateTime.UtcNow);
    }

    // ═════════════════════════════════════════════════════════════════════
    // OUTSTANDING BALANCE
    // ═════════════════════════════════════════════════════════════════════

    public async Task<OutstandingBalanceDto> GetOutstandingBalanceAsync(int loanId, DateOnly? asOfDate = null)
    {
        var loan = await _loanRepo.GetByIdAsync(loanId)
            ?? throw new KeyNotFoundException($"Gold loan {loanId} not found.");

        var asOf    = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);
        var balance = await ComputeOutstandingAsync(loan, asOf);

        return balance;
    }

    /// <summary>
    /// Core balance computation. Called by repayment recording and the public endpoint.
    /// </summary>
    private async Task<OutstandingBalanceDto> ComputeOutstandingAsync(GoldLoan loan, DateOnly asOf)
    {
        var setting = loan.InterestSetting
            ?? await _db.InterestSettings.FindAsync(loan.InterestSettingId)
            ?? throw new InvalidOperationException("Interest setting not found.");

        // Principal remaining = original - sum of all principal components of repayments
        var repayments = loan.Repayments.Any()
            ? loan.Repayments
            : await _db.LoanRepayments
                .Where(r => r.GoldLoanId == loan.Id)
                .ToListAsync();

        var totalPrincipalRepaid = repayments.Sum(r => r.PrincipalComponent);
        var principalRemaining   = loan.PrincipalAmount - totalPrincipalRepaid;
        principalRemaining       = Math.Max(0m, principalRemaining);

        if (principalRemaining == 0m)
        {
            // Fully paid
            return new OutstandingBalanceDto(
                loan.Id, loan.LoanNumber, asOf,
                loan.PrincipalAmount, loan.TotalRepaid,
                0m, 0m, 0m, 0m, 0m,
                false, 0, loan.LoanStatus.ToString(),
                loan.MaturityDate, setting.InterestRatePercent,
                setting.CalculationType.ToString(), setting.CompoundingEnabled);
        }

        // Calculate interest on PRINCIPAL REMAINING from LoanDate to asOf
        var input = new Application.DTOs.GoldLoan.InterestCalculationInput(
            Principal:         principalRemaining,
            RatePercent:       setting.InterestRatePercent,
            CalculationType:   setting.CalculationType,
            CompoundingEnabled: setting.CompoundingEnabled,
            PenaltyRatePercent: setting.PenaltyRatePercent,
            StartDate:         loan.LoanDate,
            EndDate:           asOf,
            MaturityDate:      loan.MaturityDate);

        var result = _calculator.Calculate(input);

        var totalOutstanding = principalRemaining + result.TotalInterest;
        var payoffAmount     = Math.Round(totalOutstanding, 2);

        return new OutstandingBalanceDto(
            loan.Id, loan.LoanNumber, asOf,
            loan.PrincipalAmount, loan.TotalRepaid,
            Math.Round(principalRemaining, 2),
            Math.Round(result.RegularInterest, 2),
            Math.Round(result.PenaltyInterest, 2),
            Math.Round(totalOutstanding, 2),
            payoffAmount,
            result.IsOverdue,
            result.DaysOverdue,
            loan.LoanStatus.ToString(),
            loan.MaturityDate,
            setting.InterestRatePercent,
            setting.CalculationType.ToString(),
            setting.CompoundingEnabled);
    }

    public async Task<decimal> CalculateAccruedInterestAsync(int loanId, DateOnly fromDate, DateOnly toDate)
    {
        var loan    = await _loanRepo.GetByIdAsync(loanId, false, false)
            ?? throw new KeyNotFoundException($"Loan {loanId} not found.");
        var setting = await _db.InterestSettings.FindAsync(loan.InterestSettingId)
            ?? throw new InvalidOperationException("Interest setting not found.");

        return _calculator.CalculateSimpleInterest(
            loan.PrincipalAmount, setting.InterestRatePercent,
            setting.CalculationType, fromDate, toDate);
    }

    public async Task<decimal> CalculatePenaltyInterestAsync(int loanId)
    {
        var loan    = await _loanRepo.GetByIdAsync(loanId, false, false)
            ?? throw new KeyNotFoundException($"Loan {loanId} not found.");
        var setting = await _db.InterestSettings.FindAsync(loan.InterestSettingId)
            ?? throw new InvalidOperationException("Interest setting not found.");

        return _calculator.CalculatePenaltyInterest(
            loan.PrincipalAmount, setting.PenaltyRatePercent,
            setting.CalculationType, loan.MaturityDate,
            DateOnly.FromDateTime(DateTime.Today));
    }

    // ═════════════════════════════════════════════════════════════════════
    // REPAYMENT SCHEDULE
    // ═════════════════════════════════════════════════════════════════════

    public async Task<RepaymentScheduleDto> GenerateRepaymentScheduleAsync(int loanId)
    {
        var loan = await _loanRepo.GetByIdAsync(loanId)
            ?? throw new KeyNotFoundException($"Loan {loanId} not found.");

        var setting = loan.InterestSetting
            ?? await _db.InterestSettings.FindAsync(loan.InterestSettingId)!;

        var repayments  = loan.Repayments.OrderBy(r => r.RepaymentDate).ToList();
        var engine      = (InterestCalculationEngine)_calculator;

        var rawSchedule = engine.GenerateSchedule(
            loan.PrincipalAmount, setting!.InterestRatePercent,
            setting.CalculationType, loan.LoanDate, loan.MaturityDate,
            setting.CompoundingEnabled);

        var scheduleItems = rawSchedule.Select((row, idx) =>
        {
            // Find actual repayment made in this period (if any)
            var instNum  = idx + 1;
            var periodRepayment = repayments
                .Where(r => r.RepaymentDate <= row.dueDate &&
                            r.RepaymentDate > (idx > 0 ? rawSchedule[idx - 1].dueDate : loan.LoanDate))
                .FirstOrDefault();

            return new RepaymentScheduleItemDto(
                InstallmentNumber:   instNum,
                DueDate:             row.dueDate,
                OpeningBalance:      row.openingBalance,
                InterestDue:         row.interest,
                TotalDue:            row.total,
                ClosingBalance:      row.total,
                IsPaid:              periodRepayment is not null,
                AmountActuallyPaid:  periodRepayment?.AmountPaid,
                ActualPaymentDate:   periodRepayment?.RepaymentDate);
        }).ToList();

        var totalInterest = rawSchedule.Sum(r => r.interest);

        return new RepaymentScheduleDto(
            loan.Id, loan.LoanNumber,
            loan.PrincipalAmount, totalInterest,
            loan.PrincipalAmount + totalInterest,
            loan.LoanDate, loan.MaturityDate,
            scheduleItems);
    }

    // ═════════════════════════════════════════════════════════════════════
    // LOAN LIFECYCLE ACTIONS
    // ═════════════════════════════════════════════════════════════════════

    public async Task ExtendLoanAsync(ExtendLoanDto dto, string adminUserId)
    {
        var loan = await _loanRepo.GetByIdAsync(dto.LoanId, false, false)
            ?? throw new KeyNotFoundException($"Loan {dto.LoanId} not found.");

        if (loan.LoanStatus is LoanStatus.Closed or LoanStatus.Defaulted)
            throw new InvalidOperationException($"Cannot extend a {loan.LoanStatus} loan.");

        if (dto.NewMaturityDate <= loan.MaturityDate)
            throw new InvalidOperationException(
                "New maturity date must be after the current maturity date.");

        var oldMaturity = loan.MaturityDate;

        loan.MaturityDate  = dto.NewMaturityDate;
        loan.LoanStatus    = LoanStatus.Extended;
        loan.ExtensionCount++;
        loan.Notes         = string.IsNullOrEmpty(loan.Notes)
            ? $"Extended: {dto.Reason}"
            : loan.Notes + $"\n[Extension {loan.ExtensionCount}]: {dto.Reason}";
        loan.UpdatedAt     = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = adminUserId,
            Action     = "LOAN_EXTEND",
            EntityName = "GoldLoan",
            EntityId   = loan.Id.ToString(),
            OldValues  = $"{{\"MaturityDate\":\"{oldMaturity}\",\"Status\":\"Active\"}}",
            NewValues  = $"{{\"MaturityDate\":\"{dto.NewMaturityDate}\",\"Status\":\"Extended\"," +
                         $"\"Reason\":\"{dto.Reason}\"}}",
            Timestamp  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _notificationService.SendInAppAsync(
            loan.CustomerUserId,
            $"Loan Extended — {loan.LoanNumber}",
            $"Your gold loan {loan.LoanNumber} has been extended. New maturity date: {dto.NewMaturityDate:dd-MMM-yyyy}.",
            "GoldLoan", loan.Id);

        _logger.LogInformation(
            "Loan {LoanNumber} extended by {AdminId}. Old: {Old}, New: {New}",
            loan.LoanNumber, adminUserId, oldMaturity, dto.NewMaturityDate);
    }

    public async Task CloseLoanAsync(CloseLoanDto dto, string adminUserId)
    {
        var loan = await _loanRepo.GetByIdAsync(dto.LoanId, true, true)
            ?? throw new KeyNotFoundException($"Loan {dto.LoanId} not found.");

        if (loan.LoanStatus == LoanStatus.Closed)
            throw new InvalidOperationException("Loan is already closed.");

        var oldStatus = loan.LoanStatus;
        loan.LoanStatus = LoanStatus.Closed;
        loan.ClosedDate = DateOnly.FromDateTime(DateTime.Today);
        loan.Notes = string.IsNullOrEmpty(loan.Notes) ? dto.Notes : loan.Notes + "\n" + dto.Notes;
        loan.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = adminUserId,
            Action     = "LOAN_CLOSE",
            EntityName = "GoldLoan",
            EntityId   = loan.Id.ToString(),
            OldValues  = $"{{\"Status\":\"{oldStatus}\"}}",
            NewValues  = $"{{\"Status\":\"Closed\",\"GoldReturned\":{dto.GoldReturned}," +
                         $"\"Notes\":\"{dto.Notes}\"}}",
            Timestamp  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _notificationService.SendInAppAsync(
            loan.CustomerUserId,
            $"Gold Loan Closed — {loan.LoanNumber}",
            dto.GoldReturned
                ? $"Your gold loan {loan.LoanNumber} has been closed and your gold items have been returned."
                : $"Your gold loan {loan.LoanNumber} has been closed.",
            "GoldLoan", loan.Id);
    }

    public async Task ProcessAuctionAsync(AuctionGoldDto dto, string adminUserId)
    {
        var loan = await _loanRepo.GetByIdAsync(dto.LoanId, false, true)
            ?? throw new KeyNotFoundException($"Loan {dto.LoanId} not found.");

        if (loan.LoanStatus != LoanStatus.Defaulted)
            throw new InvalidOperationException("Only Defaulted loans can be auctioned.");

        var balance = await ComputeOutstandingAsync(loan, dto.AuctionDate);
        var profitLoss = dto.AuctionProceeds - balance.TotalOutstanding;

        loan.LoanStatus = LoanStatus.Closed;
        loan.ClosedDate = dto.AuctionDate;
        loan.Notes = (loan.Notes ?? "") +
            $"\n[AUCTION {dto.AuctionDate}] Proceeds: ₹{dto.AuctionProceeds:N2} | " +
            $"Outstanding: ₹{balance.TotalOutstanding:N2} | " +
            $"P&L: ₹{profitLoss:N2} | {dto.Notes}";
        loan.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = adminUserId,
            Action     = "GOLD_AUCTION",
            EntityName = "GoldLoan",
            EntityId   = loan.Id.ToString(),
            NewValues  = $"{{\"AuctionProceeds\":{dto.AuctionProceeds}," +
                         $"\"Outstanding\":{balance.TotalOutstanding}," +
                         $"\"ProfitLoss\":{profitLoss}}}",
            Timestamp  = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogWarning(
            "Gold auctioned for defaulted loan {LoanNumber}. Proceeds: ₹{Proceeds}, P&L: ₹{PL}",
            loan.LoanNumber, dto.AuctionProceeds, profitLoss);
    }

    // ═════════════════════════════════════════════════════════════════════
    // BACKGROUND JOB — DEFAULT CHECK
    // ═════════════════════════════════════════════════════════════════════

    public async Task CheckAndMarkDefaultedLoansAsync()
    {
        var candidateLoans = await _loanRepo.GetLoansDueForDefaultCheckAsync();
        var today          = DateOnly.FromDateTime(DateTime.Today);
        int markedCount    = 0;

        foreach (var loan in candidateLoans)
        {
            var daysOverdue = today.DayNumber - loan.MaturityDate.DayNumber;
            if (daysOverdue < 90) continue;

            var oldStatus   = loan.LoanStatus;
            loan.LoanStatus = LoanStatus.Defaulted;
            loan.UpdatedAt  = DateTime.UtcNow;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId     = null,    // System action
                Action     = "LOAN_AUTO_DEFAULT",
                EntityName = "GoldLoan",
                EntityId   = loan.Id.ToString(),
                OldValues  = $"{{\"Status\":\"{oldStatus}\"}}",
                NewValues  = $"{{\"Status\":\"Defaulted\",\"DaysOverdue\":{daysOverdue}}}",
                Timestamp  = DateTime.UtcNow
            });

            // Notify customer and admin
            await _notificationService.SendInAppAsync(
                loan.CustomerUserId,
                $"Loan Defaulted — {loan.LoanNumber}",
                $"Your gold loan {loan.LoanNumber} is now marked as Defaulted " +
                $"({daysOverdue} days overdue). Please contact the shop immediately.",
                "GoldLoan", loan.Id);

            await _notificationService.NotifyAdminsAsync(
                $"Loan Auto-Defaulted: {loan.LoanNumber}",
                $"Gold loan {loan.LoanNumber} for customer {loan.Customer?.FullName} " +
                $"has been auto-marked as Defaulted ({daysOverdue} days past maturity). " +
                $"Principal: ₹{loan.PrincipalAmount:N2}.");

            markedCount++;
        }

        if (markedCount > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogWarning("DefaultCheck: Marked {Count} loans as Defaulted.", markedCount);
        }
        else
        {
            _logger.LogInformation("DefaultCheck: No loans newly defaulted today.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // QUERY METHODS
    // ═════════════════════════════════════════════════════════════════════

    public async Task<GoldLoanResponseDto> GetLoanAsync(int id)
    {
        var loan = await _loanRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Gold loan {id} not found.");
        return await MapToResponseDto(loan);
    }

    public async Task<List<GoldLoanResponseDto>> GetCustomerLoansAsync(string customerId)
    {
        var loans = await _loanRepo.GetByCustomerIdAsync(customerId);
        var result = new List<GoldLoanResponseDto>();
        foreach (var l in loans) result.Add(await MapToResponseDto(l));
        return result;
    }

    public async Task<List<GoldLoanSummaryDto>> GetActiveLoansAsync(
        int page, int pageSize, string? search = null)
    {
        var loans = await _loanRepo.GetActiveLoansAsync(page, pageSize, search);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<GoldLoanSummaryDto>();

        foreach (var l in loans)
        {
            var bal = await ComputeOutstandingAsync(l, today);
            result.Add(MapToSummaryDto(l, bal, today));
        }

        return result;
    }

    public async Task<List<GoldLoanSummaryDto>> GetDefaultedLoansAsync()
    {
        var loans = await _loanRepo.GetDefaultedLoansAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<GoldLoanSummaryDto>();
        foreach (var l in loans)
        {
            var bal = await ComputeOutstandingAsync(l, today);
            result.Add(MapToSummaryDto(l, bal, today));
        }
        return result;
    }

    public async Task<List<GoldLoanSummaryDto>> GetLoansDueThisWeekAsync()
    {
        var loans = await _loanRepo.GetLoansDueThisWeekAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<GoldLoanSummaryDto>();
        foreach (var l in loans)
        {
            var bal = await ComputeOutstandingAsync(l, today);
            result.Add(MapToSummaryDto(l, bal, today));
        }
        return result;
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    private async Task<InterestSetting> GetActiveInterestSettingAsync()
    {
        var setting = await _db.InterestSettings
            .Where(s => s.EffectiveTo == null)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "No active interest setting found. Please configure one in Settings.");
        return setting;
    }

    private async Task<GoldLoanResponseDto> MapToResponseDto(GoldLoan l)
    {
        var setting = l.InterestSetting
            ?? await _db.InterestSettings.FindAsync(l.InterestSettingId);

        return new GoldLoanResponseDto(
            l.Id, l.LoanNumber,
            l.CustomerUserId, l.Customer?.FullName ?? "", l.Customer?.CustomerCode ?? "",
            l.LoanDate, l.MaturityDate, l.ClosedDate,
            l.PrincipalAmount, l.TotalRepaid,
            l.GoldDepositedWeightGrams, l.GoldPurity,
            l.GoldCurrentValueAtDeposit, l.LoanToValuePercent,
            l.LoanStatus.ToString(),
            l.ExtensionCount, l.Notes,
            setting?.CalculationType.ToString() ?? "",
            setting?.InterestRatePercent ?? 0,
            setting?.CompoundingEnabled ?? false,
            l.GoldItems.Select(i => new GoldLoanItemResponseDto(
                i.Id, i.ItemDescription, i.WeightGrams,
                i.Purity, i.EstimatedValue, i.HallmarkNumber, i.ImageUrl)).ToList(),
            l.Repayments.OrderByDescending(r => r.RepaymentDate).Select(r => new RepaymentResponseDto(
                r.Id,
                r.ReceiptNumber,
                r.RepaymentDate,
                r.AmountPaid,
                r.PrincipalComponent,
                r.InterestComponent - r.PenaltyAmount,
                r.PenaltyAmount,
                r.PaymentMode.ToString(),
                r.PrincipalBalanceAfter,
                r.Notes,
                r.CreatedAt)).ToList(),
            l.CreatedAt);
    }

    private static GoldLoanSummaryDto MapToSummaryDto(
        GoldLoan l, OutstandingBalanceDto bal, DateOnly today)
    {
        var daysOverdue = today > l.MaturityDate
            ? today.DayNumber - l.MaturityDate.DayNumber : 0;

        return new GoldLoanSummaryDto(
            l.Id, l.LoanNumber,
            l.Customer?.FullName ?? "",
            l.Customer?.CustomerCode ?? "",
            l.LoanDate, l.MaturityDate,
            l.PrincipalAmount, l.TotalRepaid,
            bal.TotalOutstanding,
            bal.AccruedInterest,
            l.LoanStatus.ToString(),
            daysOverdue > 0, daysOverdue);
    }

    private static string BuildLoanCreatedEmailBody(
        ApplicationUser customer, GoldLoan loan, InterestSetting setting)
        => $"Dear {customer.FirstName},\n\n" +
           $"Your Gold Loan has been created successfully.\n\n" +
           $"Loan Number   : {loan.LoanNumber}\n" +
           $"Amount        : ₹{loan.PrincipalAmount:N2}\n" +
           $"Gold Deposited: {loan.GoldDepositedWeightGrams:F3}g ({loan.GoldPurity})\n" +
           $"Interest Rate : {setting.InterestRatePercent}% {setting.CalculationType}\n" +
           $"Loan Date     : {loan.LoanDate:dd-MMM-yyyy}\n" +
           $"Maturity Date : {loan.MaturityDate:dd-MMM-yyyy}\n\n" +
           $"Please repay before the maturity date to avoid penalty charges.\n\n" +
           $"Thank you,\nShree Jewellers";
}

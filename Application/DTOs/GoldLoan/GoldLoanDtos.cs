namespace ShreeJewellers.Application.DTOs.GoldLoan;

// ─────────────────────────────────────────────────────────────────────────────
// CREATE
// ─────────────────────────────────────────────────────────────────────────────

public record CreateGoldLoanDto(
    string CustomerUserId,
    DateOnly LoanDate,
    decimal PrincipalAmount,         // Cash to disburse — must be ≤ MaxEligibleAmount
    string GoldPurity,               // e.g., "22K"
    List<CreateGoldLoanItemDto> GoldItems,
    string? Notes
);

public record CreateGoldLoanItemDto(
    string ItemDescription,          // "Gold ring, floral design, 2024"
    decimal WeightGrams,
    string Purity,
    decimal EstimatedValue,
    string? HallmarkNumber,
    string? ImageBase64,             // Optional photo at pledge time
    string? ImageExtension           // e.g., "jpg"
);

// ─────────────────────────────────────────────────────────────────────────────
// RESPONSE
// ─────────────────────────────────────────────────────────────────────────────

public record GoldLoanResponseDto(
    int Id,
    string LoanNumber,
    string CustomerUserId,
    string CustomerName,
    string CustomerCode,
    DateOnly LoanDate,
    DateOnly MaturityDate,
    DateOnly? ClosedDate,
    decimal PrincipalAmount,
    decimal TotalRepaid,
    decimal GoldDepositedWeightGrams,
    string GoldPurity,
    decimal GoldValueAtDeposit,
    decimal LoanToValuePercent,
    string LoanStatus,
    int ExtensionCount,
    string? Notes,
    string InterestCalculationType,
    decimal InterestRatePercent,
    bool CompoundingEnabled,
    List<GoldLoanItemResponseDto> GoldItems,
    List<RepaymentResponseDto> Repayments,
    DateTime CreatedAt
);

public record GoldLoanItemResponseDto(
    int Id,
    string ItemDescription,
    decimal WeightGrams,
    string Purity,
    decimal EstimatedValue,
    string? HallmarkNumber,
    string? ImageUrl
);

public record GoldLoanSummaryDto(
    int Id,
    string LoanNumber,
    string CustomerName,
    string CustomerCode,
    DateOnly LoanDate,
    DateOnly MaturityDate,
    decimal PrincipalAmount,
    decimal TotalRepaid,
    decimal OutstandingBalance,
    decimal AccruedInterest,
    string LoanStatus,
    bool IsOverdue,
    int DaysOverdue
);

// ─────────────────────────────────────────────────────────────────────────────
// REPAYMENT
// ─────────────────────────────────────────────────────────────────────────────

public record RecordRepaymentDto(
    int GoldLoanId,
    DateOnly RepaymentDate,
    decimal AmountPaid,
    string PaymentMode,    // "Cash" | "Card" | "UPI" | "NEFT"
    string? Notes
);

public record RepaymentResponseDto(
    int Id,
    string ReceiptNumber,
    DateOnly RepaymentDate,
    decimal AmountPaid,
    decimal PrincipalComponent,
    decimal InterestComponent,
    decimal PenaltyAmount,
    string PaymentMode,
    decimal PrincipalBalanceAfter,
    string? Notes,
    DateTime CreatedAt
);

// ─────────────────────────────────────────────────────────────────────────────
// OUTSTANDING BALANCE
// ─────────────────────────────────────────────────────────────────────────────

public record OutstandingBalanceDto(
    int LoanId,
    string LoanNumber,
    DateOnly AsOfDate,
    decimal PrincipalAtOrigin,
    decimal TotalRepaid,
    decimal PrincipalRemaining,
    decimal AccruedInterest,
    decimal PenaltyInterest,
    decimal TotalOutstanding,           // PrincipalRemaining + AccruedInterest + PenaltyInterest
    decimal PayoffAmount,               // What the customer needs to pay today to close the loan
    bool IsOverdue,
    int DaysOverdue,
    string LoanStatus,
    DateOnly MaturityDate,
    decimal InterestRatePercent,
    string CalculationType,
    bool CompoundingEnabled
);

// ─────────────────────────────────────────────────────────────────────────────
// REPAYMENT SCHEDULE
// ─────────────────────────────────────────────────────────────────────────────

public record RepaymentScheduleItemDto(
    int InstallmentNumber,
    DateOnly DueDate,
    decimal OpeningBalance,
    decimal InterestDue,
    decimal TotalDue,                   // OpeningBalance + InterestDue
    decimal ClosingBalance,
    bool IsPaid,
    decimal? AmountActuallyPaid,
    DateOnly? ActualPaymentDate
);

public record RepaymentScheduleDto(
    int LoanId,
    string LoanNumber,
    decimal PrincipalAmount,
    decimal TotalInterestIfPaidOnSchedule,
    decimal TotalPayableIfPaidOnSchedule,
    DateOnly LoanDate,
    DateOnly MaturityDate,
    List<RepaymentScheduleItemDto> Schedule
);

// ─────────────────────────────────────────────────────────────────────────────
// LOAN MANAGEMENT ACTIONS
// ─────────────────────────────────────────────────────────────────────────────

public record ExtendLoanDto(
    int LoanId,
    DateOnly NewMaturityDate,
    string Reason
);

public record CloseLoanDto(
    int LoanId,
    string Notes,
    bool GoldReturned = true
);

public record AuctionGoldDto(
    int LoanId,
    decimal AuctionProceeds,
    string Notes,
    DateOnly AuctionDate
);

// ─────────────────────────────────────────────────────────────────────────────
// LOAN ELIGIBILITY (before creation)
// ─────────────────────────────────────────────────────────────────────────────

public record LoanEligibilityRequestDto(
    string CustomerUserId,
    List<CreateGoldLoanItemDto> GoldItems
);

public record LoanEligibilityResponseDto(
    bool IsEligible,
    string? IneligibilityReason,
    decimal TotalGoldWeightGrams,
    decimal TotalGoldMarketValue,
    decimal LoanToValuePercent,
    decimal MaxEligibleLoanAmount,
    decimal CurrentGoldRatePer10g,
    string GoldPurityAssumed,
    decimal InterestRatePercent,
    string InterestCalculationType,
    DateOnly ProposedMaturityDate
);

// ─────────────────────────────────────────────────────────────────────────────
// INTEREST CALCULATION (internal engine types)
// ─────────────────────────────────────────────────────────────────────────────

public record InterestCalculationInput(
    decimal Principal,
    decimal RatePercent,
    ShreeJewellers.Domain.Enums.InterestCalculationType CalculationType,
    bool CompoundingEnabled,
    decimal PenaltyRatePercent,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly MaturityDate
);

public record InterestCalculationResult(
    decimal RegularInterest,
    decimal PenaltyInterest,
    decimal TotalInterest,
    decimal TotalOutstanding,        // Principal + TotalInterest
    int TotalDays,
    bool IsOverdue,
    int DaysOverdue,
    string CalculationBreakdown     // Human-readable explanation for admin UI
);

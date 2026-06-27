using ShreeJewellers.Application.DTOs.GoldLoan;
using ShreeJewellers.Domain.Enums;
using System.Text;

namespace ShreeJewellers.Infrastructure.Services;

public interface IInterestCalculationEngine
{
    InterestCalculationResult Calculate(InterestCalculationInput input);

    decimal CalculateSimpleInterest(decimal principal, decimal ratePercent,
        InterestCalculationType type, DateOnly from, DateOnly to);

    decimal CalculateCompoundInterest(decimal principal, decimal ratePercent,
        InterestCalculationType type, DateOnly from, DateOnly to);

    decimal CalculatePenaltyInterest(decimal principal, decimal penaltyRatePercent,
        InterestCalculationType type, DateOnly maturityDate, DateOnly asOfDate);

    /// <summary>
    /// Full calculation combining regular + penalty interest with optional compounding.
    /// This is the canonical method used by GoldLoanService for all balance queries.
    /// </summary>
    (decimal regular, decimal penalty) CalculateSplit(InterestCalculationInput input);
}

/// <summary>
/// Pure, stateless interest calculation engine.
/// Has ZERO dependencies — designed for isolated unit testing.
///
/// FORMULAS:
///   Simple Daily:   P × (R/100) × D           where D = days
///   Simple Monthly: P × (R/100) × M           where M = months (fractional)
///   Simple Yearly:  P × (R/100) × Y           where Y = years  (fractional)
///   Compound:       P × ((1 + R/100)^periods) - P  (compounded at period frequency)
///   Penalty:        same formula applied to overdue period only, using penaltyRate
/// </summary>
public class InterestCalculationEngine : IInterestCalculationEngine
{
    // ── Main Entry Point ──────────────────────────────────────────────────

    public InterestCalculationResult Calculate(InterestCalculationInput input)
    {
        ValidateInput(input);

        var today        = input.EndDate;
        var isOverdue    = today > input.MaturityDate;
        var daysOverdue  = isOverdue ? (today.DayNumber - input.MaturityDate.DayNumber) : 0;
        var totalDays    = today.DayNumber - input.StartDate.DayNumber;

        var (regular, penalty) = CalculateSplit(input);
        var total = regular + penalty;

        var breakdown = BuildBreakdown(input, regular, penalty, isOverdue, daysOverdue);

        return new InterestCalculationResult(
            RegularInterest:    Math.Round(regular, 2),
            PenaltyInterest:    Math.Round(penalty, 2),
            TotalInterest:      Math.Round(total,   2),
            TotalOutstanding:   Math.Round(input.Principal + total, 2),
            TotalDays:          totalDays,
            IsOverdue:          isOverdue,
            DaysOverdue:        daysOverdue,
            CalculationBreakdown: breakdown
        );
    }

    // ── Split Calculation (regular + penalty) ────────────────────────────

    public (decimal regular, decimal penalty) CalculateSplit(InterestCalculationInput input)
    {
        var isOverdue = input.EndDate > input.MaturityDate;

        // Period for regular interest: StartDate → min(EndDate, MaturityDate)
        var regularEndDate = isOverdue ? input.MaturityDate : input.EndDate;

        decimal regular = input.CompoundingEnabled
            ? CalculateCompoundInterest(
                input.Principal, input.RatePercent,
                input.CalculationType, input.StartDate, regularEndDate)
            : CalculateSimpleInterest(
                input.Principal, input.RatePercent,
                input.CalculationType, input.StartDate, regularEndDate);

        // Penalty interest on the PRINCIPAL (not compounded principal) for overdue period
        decimal penalty = 0m;
        if (isOverdue && input.PenaltyRatePercent > 0)
        {
            penalty = CalculatePenaltyInterest(
                input.Principal, input.PenaltyRatePercent,
                input.CalculationType, input.MaturityDate, input.EndDate);
        }

        return (regular, penalty);
    }

    // ── Simple Interest ───────────────────────────────────────────────────

    public decimal CalculateSimpleInterest(
        decimal principal, decimal ratePercent,
        InterestCalculationType type, DateOnly from, DateOnly to)
    {
        if (from >= to || principal <= 0 || ratePercent <= 0) return 0m;

        var rate = ratePercent / 100m;

        return type switch
        {
            InterestCalculationType.Daily =>
                principal * rate * DaysBetween(from, to),

            InterestCalculationType.Monthly =>
                principal * rate * MonthsBetween(from, to),

            InterestCalculationType.Yearly =>
                principal * rate * YearsBetween(from, to),

            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown type: {type}")
        };
    }

    // ── Compound Interest ─────────────────────────────────────────────────

    public decimal CalculateCompoundInterest(
        decimal principal, decimal ratePercent,
        InterestCalculationType type, DateOnly from, DateOnly to)
    {
        if (from >= to || principal <= 0 || ratePercent <= 0) return 0m;

        // Compounding period = the calculation type (monthly compounding for monthly rate)
        var periods = type switch
        {
            InterestCalculationType.Daily   => (decimal)DaysBetween(from, to),
            InterestCalculationType.Monthly => MonthsBetween(from, to),
            InterestCalculationType.Yearly  => YearsBetween(from, to),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // A = P × (1 + r)^n   →   Interest = A - P
        var rate = ratePercent / 100m;
        var compoundedAmount = principal * (decimal)Math.Pow((double)(1m + rate), (double)periods);
        return compoundedAmount - principal;
    }

    // ── Penalty Interest ─────────────────────────────────────────────────

    public decimal CalculatePenaltyInterest(
        decimal principal, decimal penaltyRatePercent,
        InterestCalculationType type, DateOnly maturityDate, DateOnly asOfDate)
    {
        if (asOfDate <= maturityDate || penaltyRatePercent <= 0) return 0m;

        // Penalty uses SIMPLE interest (not compounded) on overdue period
        return CalculateSimpleInterest(
            principal, penaltyRatePercent, type, maturityDate, asOfDate);
    }

    // ── Period Helpers ────────────────────────────────────────────────────

    /// <summary>Exact days between two dates.</summary>
    private static decimal DaysBetween(DateOnly from, DateOnly to)
        => to.DayNumber - from.DayNumber;

    /// <summary>
    /// Fractional months between two dates.
    /// Uses full calendar months + remaining days as fraction of 30.
    /// e.g., 1 month 15 days = 1.5 months
    /// </summary>
    private static decimal MonthsBetween(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0m;

        int fullMonths  = ((to.Year - from.Year) * 12) + (to.Month - from.Month);
        var lastFullMonth = from.AddMonths(fullMonths);

        // If we overshot (e.g., from=Jan 31, adding month gives Feb 28 which is before March 1)
        if (lastFullMonth > to)
        {
            fullMonths--;
            lastFullMonth = from.AddMonths(fullMonths);
        }

        var remainingDays = to.DayNumber - lastFullMonth.DayNumber;
        var fractionalMonth = remainingDays / 30m;

        return fullMonths + fractionalMonth;
    }

    /// <summary>Fractional years between two dates (days / 365).</summary>
    private static decimal YearsBetween(DateOnly from, DateOnly to)
        => DaysBetween(from, to) / 365m;

    // ── Amortization (for repayment schedule projection) ─────────────────

    /// <summary>
    /// Generates a month-by-month repayment schedule showing how interest
    /// accrues each period and the cumulative outstanding balance.
    /// </summary>
    public List<(DateOnly dueDate, decimal openingBalance, decimal interest, decimal total)>
        GenerateSchedule(
            decimal principal, decimal ratePercent,
            InterestCalculationType type, DateOnly startDate, DateOnly maturityDate,
            bool compounding)
    {
        var schedule = new List<(DateOnly, decimal, decimal, decimal)>();
        var currentDate = startDate;
        var currentPrincipal = principal;

        // Number of periods based on type
        var totalPeriods = type switch
        {
            InterestCalculationType.Monthly =>
                (int)Math.Ceiling((double)MonthsBetween(startDate, maturityDate)),
            InterestCalculationType.Yearly  =>
                (int)Math.Ceiling((double)YearsBetween(startDate, maturityDate)),
            InterestCalculationType.Daily   =>
                (int)DaysBetween(startDate, maturityDate),
            _ => 12
        };

        for (int i = 1; i <= totalPeriods; i++)
        {
            var nextDate = type switch
            {
                InterestCalculationType.Monthly => currentDate.AddMonths(1),
                InterestCalculationType.Yearly  => currentDate.AddYears(1),
                InterestCalculationType.Daily   => currentDate.AddDays(1),
                _ => currentDate.AddMonths(1)
            };
            if (nextDate > maturityDate) nextDate = maturityDate;

            var interest = compounding
                ? CalculateCompoundInterest(currentPrincipal, ratePercent, type, currentDate, nextDate)
                : CalculateSimpleInterest(currentPrincipal, ratePercent, type, currentDate, nextDate);

            interest = Math.Round(interest, 2);
            var total = currentPrincipal + interest;

            schedule.Add((nextDate, currentPrincipal, interest, Math.Round(total, 2)));

            if (compounding)
                currentPrincipal = total;     // Unpaid interest rolls into principal

            currentDate = nextDate;
            if (currentDate >= maturityDate) break;
        }

        return schedule;
    }

    // ── Validation ────────────────────────────────────────────────────────

    private static void ValidateInput(InterestCalculationInput input)
    {
        if (input.Principal < 0)
            throw new ArgumentException("Principal cannot be negative.", nameof(input));
        if (input.RatePercent < 0)
            throw new ArgumentException("Interest rate cannot be negative.", nameof(input));
        if (input.EndDate < input.StartDate)
            throw new ArgumentException("End date must be after start date.", nameof(input));
    }

    // ── Human-Readable Breakdown ─────────────────────────────────────────

    private static string BuildBreakdown(
        InterestCalculationInput input,
        decimal regular, decimal penalty,
        bool isOverdue, int daysOverdue)
    {
        var sb = new StringBuilder();
        var totalDays = input.EndDate.DayNumber - input.StartDate.DayNumber;

        sb.AppendLine($"Principal: ₹{input.Principal:N2}");
        sb.AppendLine($"Interest Rate: {input.RatePercent}% {input.CalculationType}");
        sb.AppendLine($"Compounding: {(input.CompoundingEnabled ? "Yes" : "No")}");
        sb.AppendLine($"Loan Period: {input.StartDate:dd-MMM-yyyy} to {input.EndDate:dd-MMM-yyyy} ({totalDays} days)");
        sb.AppendLine($"Regular Interest ({input.StartDate:dd-MMM-yyyy} → {(isOverdue ? input.MaturityDate : input.EndDate):dd-MMM-yyyy}): ₹{regular:N2}");

        if (isOverdue)
        {
            sb.AppendLine($"Overdue: {daysOverdue} days past {input.MaturityDate:dd-MMM-yyyy}");
            sb.AppendLine($"Penalty Rate: {input.PenaltyRatePercent}% {input.CalculationType}");
            sb.AppendLine($"Penalty Interest: ₹{penalty:N2}");
        }

        sb.AppendLine($"Total Interest: ₹{(regular + penalty):N2}");
        sb.AppendLine($"Total Outstanding: ₹{(input.Principal + regular + penalty):N2}");

        return sb.ToString().TrimEnd();
    }
}

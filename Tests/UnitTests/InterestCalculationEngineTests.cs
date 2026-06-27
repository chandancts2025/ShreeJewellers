using FluentAssertions;
using ShreeJewellers.Application.DTOs.GoldLoan;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Services;
using Xunit;

namespace ShreeJewellers.Tests.UnitTests;

/// <summary>
/// Full test coverage for InterestCalculationEngine.
/// Tests are intentionally dependency-free — no DB, no mocks, no DI.
/// Each test documents a real business scenario with manually verified expected values.
/// </summary>
public class InterestCalculationEngineTests
{
    private readonly InterestCalculationEngine _engine = new();

    // ═════════════════════════════════════════════════════════════════════
    // 1. SIMPLE INTEREST — MONTHLY
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void SimpleMonthly_1Month_CorrectInterest()
    {
        // ₹50,000 × 2% × 1 month = ₹1,000
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));

        interest.Should().BeApproximately(1_000m, 1m);
    }

    [Fact]
    public void SimpleMonthly_3Months_CorrectInterest()
    {
        // ₹50,000 × 2% × 3 months = ₹3,000
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 4, 1));

        interest.Should().BeApproximately(3_000m, 1m);
    }

    [Fact]
    public void SimpleMonthly_6Months_CorrectInterest()
    {
        // ₹50,000 × 2% × 6 = ₹6,000
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 7, 1));

        interest.Should().BeApproximately(6_000m, 1m);
    }

    [Fact]
    public void SimpleMonthly_12Months_CorrectInterest()
    {
        // ₹50,000 × 2% × 12 = ₹12,000 (24% per annum)
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        interest.Should().BeApproximately(12_000m, 5m);
    }

    [Fact]
    public void SimpleMonthly_FractionalMonths_CorrectInterest()
    {
        // 1 month + 15 days = 1.5 months → ₹50,000 × 2% × 1.5 = ₹1,500
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 16));  // 46 days ≈ 1.5 months

        interest.Should().BeGreaterThan(1_000m).And.BeLessThan(2_000m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 2. SIMPLE INTEREST — DAILY
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void SimpleDaily_30Days_CorrectInterest()
    {
        // ₹50,000 × 0.07% × 30 = ₹1,050  (0.07% daily = ~25.5% p.a.)
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 0.07m, InterestCalculationType.Daily,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));

        interest.Should().BeApproximately(1_050m, 5m);
    }

    [Fact]
    public void SimpleDaily_365Days_CorrectInterest()
    {
        // ₹50,000 × 0.06849% × 365 = ₹12,500  (0.06849% = 25%/365 daily)
        // Actually using 2%/30 daily = 0.0667% per day
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 0.0667m, InterestCalculationType.Daily,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));  // 366 days (leap year)

        interest.Should().BeGreaterThan(10_000m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 3. SIMPLE INTEREST — YEARLY
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void SimpleYearly_1Year_CorrectInterest()
    {
        // ₹50,000 × 24% × 1 year = ₹12,000
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 24m, InterestCalculationType.Yearly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        interest.Should().BeApproximately(12_000m, 50m);
    }

    [Fact]
    public void SimpleYearly_HalfYear_CorrectInterest()
    {
        // ₹50,000 × 24% × 0.5 year = ₹6,000
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 24m, InterestCalculationType.Yearly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 7, 1));

        interest.Should().BeApproximately(6_000m, 100m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 4. COMPOUND INTEREST — MONTHLY
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void CompoundMonthly_12Months_HigherThanSimple()
    {
        var simple   = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        var compound = _engine.CalculateCompoundInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        compound.Should().BeGreaterThan(simple,
            "compound interest must always exceed simple interest over the same period");
    }

    [Fact]
    public void CompoundMonthly_1Month_SameAsSimple()
    {
        // For exactly 1 period, compound = simple
        var simple   = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));

        var compound = _engine.CalculateCompoundInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));

        compound.Should().BeApproximately(simple, 1m);
    }

    [Fact]
    public void CompoundMonthly_12Months_FormulaCorrect()
    {
        // A = 50000 × (1.02)^12 = 50000 × 1.26824 = 63412 → Interest = 13412
        var compound = _engine.CalculateCompoundInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        // (1.02)^12 ≈ 1.26824
        var expected = 50_000m * ((decimal)Math.Pow(1.02, 12) - 1);
        compound.Should().BeApproximately(expected, 5m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 5. PENALTY INTEREST
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Penalty_NotOverdue_ReturnsZero()
    {
        var penalty = _engine.CalculatePenaltyInterest(
            50_000m, 1m, InterestCalculationType.Monthly,
            new DateOnly(2024, 12, 1),   // maturity
            new DateOnly(2024, 11, 30)); // before maturity

        penalty.Should().Be(0m, "no penalty before maturity date");
    }

    [Fact]
    public void Penalty_OnMaturityDate_ReturnsZero()
    {
        var penalty = _engine.CalculatePenaltyInterest(
            50_000m, 1m, InterestCalculationType.Monthly,
            new DateOnly(2024, 12, 1),
            new DateOnly(2024, 12, 1));  // same as maturity

        penalty.Should().Be(0m, "no penalty on the exact maturity date");
    }

    [Fact]
    public void Penalty_1MonthOverdue_CorrectPenalty()
    {
        // ₹50,000 × 1% × 1 month = ₹500
        var penalty = _engine.CalculatePenaltyInterest(
            50_000m, 1m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 2, 1));

        penalty.Should().BeApproximately(500m, 1m);
    }

    [Fact]
    public void Penalty_3MonthsOverdue_CorrectPenalty()
    {
        // ₹50,000 × 1% × 3 = ₹1,500
        var penalty = _engine.CalculatePenaltyInterest(
            50_000m, 1m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 4, 1));

        penalty.Should().BeApproximately(1_500m, 5m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 6. FULL CALCULATION (Calculate method)
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_ActiveLoan_NoPenalty()
    {
        var result = _engine.Calculate(new InterestCalculationInput(
            Principal:          50_000m,
            RatePercent:        2m,
            CalculationType:    InterestCalculationType.Monthly,
            CompoundingEnabled: false,
            PenaltyRatePercent: 1m,
            StartDate:          new DateOnly(2024, 1, 1),
            EndDate:            new DateOnly(2024, 7, 1),  // 6 months, within maturity
            MaturityDate:       new DateOnly(2025, 1, 1)   // maturity is 1 year
        ));

        result.PenaltyInterest.Should().Be(0m);
        result.IsOverdue.Should().BeFalse();
        result.RegularInterest.Should().BeApproximately(6_000m, 5m);
    }

    [Fact]
    public void Calculate_OverdueLoan_HasPenalty()
    {
        var result = _engine.Calculate(new InterestCalculationInput(
            Principal:          50_000m,
            RatePercent:        2m,
            CalculationType:    InterestCalculationType.Monthly,
            CompoundingEnabled: false,
            PenaltyRatePercent: 1m,
            StartDate:          new DateOnly(2024, 1, 1),
            EndDate:            new DateOnly(2025, 4, 1),  // 3 months past maturity
            MaturityDate:       new DateOnly(2025, 1, 1)
        ));

        result.IsOverdue.Should().BeTrue();
        result.DaysOverdue.Should().BeGreaterThan(0);
        result.PenaltyInterest.Should().BeGreaterThan(0m);

        // Regular: 12 months × 2% = 12,000
        result.RegularInterest.Should().BeApproximately(12_000m, 20m);
        // Penalty: 3 months × 1% × 50,000 = 1,500
        result.PenaltyInterest.Should().BeApproximately(1_500m, 10m);
    }

    [Fact]
    public void Calculate_TotalOutstanding_SumOfComponents()
    {
        var result = _engine.Calculate(new InterestCalculationInput(
            50_000m, 2m, InterestCalculationType.Monthly,
            false, 1m,
            new DateOnly(2024, 1, 1),
            new DateOnly(2025, 4, 1),
            new DateOnly(2025, 1, 1)));

        result.TotalOutstanding.Should().BeApproximately(
            50_000m + result.RegularInterest + result.PenaltyInterest, 1m,
            "TotalOutstanding must equal Principal + RegularInterest + PenaltyInterest");
    }

    [Fact]
    public void Calculate_BreakdownIsNotEmpty()
    {
        var result = _engine.Calculate(new InterestCalculationInput(
            50_000m, 2m, InterestCalculationType.Monthly, false, 1m,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 7, 1), new DateOnly(2025, 1, 1)));

        result.CalculationBreakdown.Should().NotBeNullOrEmpty();
        result.CalculationBreakdown.Should().Contain("₹");
    }

    // ═════════════════════════════════════════════════════════════════════
    // 7. SCENARIO: ₹50,000 LOAN, 2%/MONTH, 3 PARTIAL REPAYMENTS
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_50K_3PartialRepayments_InterestReducesCorrectly()
    {
        // Loan: ₹50,000 on Jan 1 at 2%/month. Maturity: Jan 1 next year.
        // Repayment 1: ₹5,000 on Mar 1 (2 months → ₹2,000 interest, ₹3,000 to principal)
        // Repayment 2: ₹10,000 on Jun 1 (3 months on ₹47,000 principal → ₹2,820)
        // Repayment 3: ₹20,000 on Sep 1 (3 months on ₹39,820 principal → ₹2,389)

        decimal principal = 50_000m;

        // After repayment 1: 2 months interest on 50,000
        var interest1 = _engine.CalculateSimpleInterest(
            principal, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 1));
        interest1.Should().BeApproximately(2_000m, 5m);

        var repay1 = 5_000m;
        var principalCleared1 = repay1 - interest1;
        principalCleared1.Should().BeApproximately(3_000m, 5m);
        principal -= principalCleared1;
        principal.Should().BeApproximately(47_000m, 5m);

        // After repayment 2: 3 months on ₹47,000
        var interest2 = _engine.CalculateSimpleInterest(
            principal, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 3, 1), new DateOnly(2024, 6, 1));
        interest2.Should().BeApproximately(2_820m, 10m);

        var repay2 = 10_000m;
        var principalCleared2 = repay2 - interest2;
        principal -= principalCleared2;
        principal.Should().BeGreaterThan(30_000m).And.BeLessThan(50_000m);

        // After repayment 3: verify interest is less than on original principal
        var interest3 = _engine.CalculateSimpleInterest(
            principal, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 6, 1), new DateOnly(2024, 9, 1));
        interest3.Should().BeLessThan(interest2,
            "interest should decrease as principal is repaid");
    }

    // ═════════════════════════════════════════════════════════════════════
    // 8. SCENARIO: OVERDUE LOAN WITH PENALTY
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_OverdueLoan_PenaltyAccumulatesCorrectly()
    {
        // Loan: ₹1,00,000 at 2%/month. Maturity: Jan 1, 2025.
        // Check on April 1, 2025 (3 months overdue).
        var result = _engine.Calculate(new InterestCalculationInput(
            Principal:          1_00_000m,
            RatePercent:        2m,
            CalculationType:    InterestCalculationType.Monthly,
            CompoundingEnabled: false,
            PenaltyRatePercent: 1m,
            StartDate:          new DateOnly(2024, 1, 1),
            EndDate:            new DateOnly(2025, 4, 1),
            MaturityDate:       new DateOnly(2025, 1, 1)));

        result.IsOverdue.Should().BeTrue();
        result.DaysOverdue.Should().BeGreaterThanOrEqualTo(90);

        // Regular: 12 months × 2% × 1,00,000 = ₹24,000
        result.RegularInterest.Should().BeApproximately(24_000m, 50m);

        // Penalty: 3 months × 1% × 1,00,000 = ₹3,000
        result.PenaltyInterest.Should().BeApproximately(3_000m, 20m);

        // Total outstanding: ₹1,00,000 + ₹24,000 + ₹3,000 = ₹1,27,000
        result.TotalOutstanding.Should().BeApproximately(1_27_000m, 100m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 9. SCENARIO: EARLY REPAYMENT
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_EarlyRepayment_LessInterestThanFullTerm()
    {
        // Loan: ₹50,000 at 2%/month for 12 months.
        // If paid at 3 months, interest should be far less than 12-month interest.

        var interestAt3Months  = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 4, 1));

        var interestAt12Months = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        interestAt3Months.Should().BeApproximately(3_000m, 10m);
        interestAt12Months.Should().BeApproximately(12_000m, 20m);

        interestAt3Months.Should().BeLessThan(interestAt12Months);

        // Saving by paying early:
        var saving = interestAt12Months - interestAt3Months;
        saving.Should().BeApproximately(9_000m, 30m);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 10. EDGE CASES
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void EdgeCase_ZeroPrincipal_ReturnsZeroInterest()
    {
        var interest = _engine.CalculateSimpleInterest(
            0m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 7, 1));

        interest.Should().Be(0m);
    }

    [Fact]
    public void EdgeCase_ZeroRate_ReturnsZeroInterest()
    {
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 0m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 7, 1));

        interest.Should().Be(0m);
    }

    [Fact]
    public void EdgeCase_SameDates_ReturnsZeroInterest()
    {
        var date = new DateOnly(2024, 6, 15);
        var interest = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly, date, date);

        interest.Should().Be(0m, "no time elapsed means no interest");
    }

    [Fact]
    public void EdgeCase_PaidOnLoanDate_BalanceEqualsPrincipal()
    {
        // If loan is repaid on the same day, total outstanding = principal (no interest)
        var result = _engine.Calculate(new InterestCalculationInput(
            50_000m, 2m, InterestCalculationType.Monthly, false, 1m,
            StartDate:   new DateOnly(2024, 1, 1),
            EndDate:     new DateOnly(2024, 1, 1),
            MaturityDate: new DateOnly(2025, 1, 1)));

        result.TotalInterest.Should().Be(0m);
        result.TotalOutstanding.Should().Be(50_000m);
    }

    [Fact]
    public void EdgeCase_VerySmallLoan_StillCalculatesCorrectly()
    {
        // ₹1,000 at 2%/month for 1 month = ₹20
        var interest = _engine.CalculateSimpleInterest(
            1_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));

        interest.Should().BeApproximately(20m, 1m);
    }

    [Fact]
    public void EdgeCase_LargeLoan_PrecisionMaintained()
    {
        // ₹50,00,000 at 2%/month for 12 months = ₹12,00,000
        var interest = _engine.CalculateSimpleInterest(
            50_00_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        interest.Should().BeApproximately(12_00_000m, 500m);
    }

    [Fact]
    public void EdgeCase_CalculationType_MonthlyVsDaily_Different()
    {
        // 30-day period: monthly 2% vs daily 2% should give different results
        var monthly = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));

        var daily = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Daily,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));

        // Daily 2% for 31 days = 50000 × 0.02 × 31 = 31,000 (MUCH higher than monthly)
        daily.Should().BeGreaterThan(monthly);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 11. REPAYMENT SCHEDULE GENERATION
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Schedule_12Months_Generates12Entries()
    {
        var schedule = _engine.GenerateSchedule(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1),
            compounding: false);

        schedule.Should().HaveCount(12);
    }

    [Fact]
    public void Schedule_EachEntry_InterestIsPositive()
    {
        var schedule = _engine.GenerateSchedule(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1),
            compounding: false);

        schedule.Should().AllSatisfy(entry =>
            entry.interest.Should().BeGreaterThan(0m));
    }

    [Fact]
    public void Schedule_TotalInterest_MatchesSimpleCalculation()
    {
        var schedule     = _engine.GenerateSchedule(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1),
            compounding: false);

        var totalInterest = schedule.Sum(s => s.interest);

        var expected = _engine.CalculateSimpleInterest(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        totalInterest.Should().BeApproximately(expected, 10m);
    }

    [Fact]
    public void Schedule_Compounding_TotalHigherThanSimple()
    {
        var simpleSchedule = _engine.GenerateSchedule(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1), compounding: false);

        var compoundSchedule = _engine.GenerateSchedule(
            50_000m, 2m, InterestCalculationType.Monthly,
            new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1), compounding: true);

        var simpleTotal   = simpleSchedule.Sum(s => s.interest);
        var compoundTotal = compoundSchedule.Sum(s => s.interest);

        compoundTotal.Should().BeGreaterThan(simpleTotal);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 12. INPUT VALIDATION
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_EndDateBeforeStartDate_ThrowsArgumentException()
    {
        var action = () => _engine.Calculate(new InterestCalculationInput(
            50_000m, 2m, InterestCalculationType.Monthly, false, 1m,
            new DateOnly(2024, 6, 1),   // StartDate
            new DateOnly(2024, 1, 1),   // EndDate BEFORE StartDate
            new DateOnly(2025, 1, 1)));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*End date must be after start date*");
    }

    [Fact]
    public void Calculate_NegativePrincipal_ThrowsArgumentException()
    {
        var action = () => _engine.Calculate(new InterestCalculationInput(
            -50_000m, 2m, InterestCalculationType.Monthly, false, 1m,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 7, 1), new DateOnly(2025, 1, 1)));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Principal cannot be negative*");
    }
}

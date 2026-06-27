using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ShreeJewellers.Application.DTOs.GoldLoan;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Repositories;
using ShreeJewellers.Infrastructure.Services;
using Xunit;

namespace ShreeJewellers.Tests.UnitTests;

/// <summary>
/// GoldLoanService unit tests using Moq for all dependencies.
/// Database interactions are replaced with InMemory EF Core provider.
/// </summary>
public class GoldLoanServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IGoldLoanRepository> _loanRepoMock;
    private readonly Mock<IInterestCalculationEngine> _calcMock;
    private readonly Mock<IPriceService> _priceMock;
    private readonly Mock<INotificationService> _notifMock;
    private readonly Mock<ILogger<GoldLoanService>> _loggerMock;
    private readonly GoldLoanService _sut;

    // ── Shared test data ──────────────────────────────────────
    private const string VerifiedCustomerId   = "verified-customer-001";
    private const string UnverifiedCustomerId = "unverified-customer-002";
    private const string AdminUserId          = "admin-user-001";

    public GoldLoanServiceTests()
    {
        // InMemory DB for entities that must actually be queried
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        _loanRepoMock = new Mock<IGoldLoanRepository>();
        _calcMock     = new Mock<IInterestCalculationEngine>();
        _priceMock    = new Mock<IPriceService>();
        _notifMock    = new Mock<INotificationService>();
        _loggerMock   = new Mock<ILogger<GoldLoanService>>();

        _sut = new GoldLoanService(
            _loanRepoMock.Object,
            _calcMock.Object,
            _priceMock.Object,
            _notifMock.Object,
            _db,
            _loggerMock.Object);

        SeedDatabase();
    }

    private void SeedDatabase()
    {
        // Verified customer
        _db.Users.Add(new ApplicationUser
        {
            Id           = VerifiedCustomerId,
            FirstName    = "Priya",
            LastName     = "Sharma",
            Email        = "priya@test.com",
            UserName     = "priya@test.com",
            KYCStatus    = KYCStatus.Verified,
            IsActive     = true,
            CustomerCode = "CUST-2024-0001",
            DateOfBirth  = new DateOnly(1990, 1, 1),
            Gender       = "Female",
            AddressLine1 = "123 Test St",
            City = "Mumbai", State = "Maharashtra", PinCode = "400001"
        });

        // Unverified customer
        _db.Users.Add(new ApplicationUser
        {
            Id           = UnverifiedCustomerId,
            FirstName    = "Rahul",
            LastName     = "Kumar",
            Email        = "rahul@test.com",
            UserName     = "rahul@test.com",
            KYCStatus    = KYCStatus.Pending,
            IsActive     = true,
            CustomerCode = "CUST-2024-0002",
            DateOfBirth  = new DateOnly(1992, 5, 15),
            Gender       = "Male",
            AddressLine1 = "456 Test Ave",
            City = "Mumbai", State = "Maharashtra", PinCode = "400002"
        });

        // Default interest setting
        _db.InterestSettings.Add(new InterestSetting
        {
            Id                  = 1,
            InterestRatePercent = 2.00m,
            CalculationType     = InterestCalculationType.Monthly,
            CompoundingEnabled  = false,
            PenaltyRatePercent  = 1.00m,
            DefaultTenureMonths = 12,
            LoanToValuePercent  = 75.00m,
            EffectiveFrom       = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            EffectiveTo         = null,
            CreatedByUserId     = AdminUserId,
            CreatedAt           = DateTime.UtcNow
        });

        _db.SaveChanges();

        // Default price mock
        _priceMock.Setup(p => p.GetCurrentRateAsync())
            .ReturnsAsync(new GoldPriceDto(6500m, 7100m, 95000m, DateTime.UtcNow, false));
    }

    // ═══════════════════════════════════════════════════════
    // CREATE LOAN TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CreateLoan_WithVerifiedKYC_CreatesSuccessfully()
    {
        // Arrange
        var dto = BuildCreateLoanDto(VerifiedCustomerId, principalAmount: 50_000m);
        SetupLoanRepoForCreate();

        // Act
        var result = await _sut.CreateLoanAsync(dto, AdminUserId);

        // Assert
        result.Should().NotBeNull();
        result.LoanNumber.Should().StartWith("GL-");
        result.PrincipalAmount.Should().Be(50_000m);
        result.CustomerUserId.Should().Be(VerifiedCustomerId);

        _loanRepoMock.Verify(r => r.AddAsync(It.IsAny<GoldLoan>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateLoan_KYCNotVerified_ThrowsInvalidOperationException()
    {
        // Arrange — customer with Pending KYC
        var dto = BuildCreateLoanDto(UnverifiedCustomerId, principalAmount: 30_000m);

        // Act
        var act = async () => await _sut.CreateLoanAsync(dto, AdminUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*KYC*");

        _loanRepoMock.Verify(r => r.AddAsync(It.IsAny<GoldLoan>()), Times.Never);
    }

    [Fact]
    public async Task CreateLoan_InactiveCustomer_ThrowsInvalidOperationException()
    {
        // Arrange — make customer inactive
        var customer = await _db.Users.FindAsync(VerifiedCustomerId);
        customer!.IsActive = false;
        await _db.SaveChangesAsync();

        var dto = BuildCreateLoanDto(VerifiedCustomerId, principalAmount: 10_000m);

        // Act & Assert
        var act = async () => await _sut.CreateLoanAsync(dto, AdminUserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task CreateLoan_ExceedsLTVLimit_ThrowsInvalidOperationException()
    {
        // Arrange: gold weight 100g × 22K rate (₹650/g) = ₹65,000 × 75% = ₹48,750 max
        // Request ₹60,000 — exceeds limit
        var dto = BuildCreateLoanDto(VerifiedCustomerId, principalAmount: 60_000m, goldWeightGrams: 100m);

        // Act & Assert
        var act = async () => await _sut.CreateLoanAsync(dto, AdminUserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum eligible*");
    }

    [Fact]
    public async Task CreateLoan_EmptyGoldItems_ThrowsInvalidOperationException()
    {
        var dto = new CreateGoldLoanDto(
            CustomerUserId: VerifiedCustomerId,
            LoanDate:       DateOnly.FromDateTime(DateTime.Today),
            PrincipalAmount: 10_000m,
            GoldPurity:     "22K",
            GoldItems:      new List<CreateGoldLoanItemDto>(),
            Notes:          null);

        var act = async () => await _sut.CreateLoanAsync(dto, AdminUserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one gold item*");
    }

    [Fact]
    public async Task CreateLoan_SendsNotificationToCustomer()
    {
        // Arrange
        var dto = BuildCreateLoanDto(VerifiedCustomerId, principalAmount: 25_000m);
        SetupLoanRepoForCreate();

        // Act
        await _sut.CreateLoanAsync(dto, AdminUserId);

        // Assert
        _notifMock.Verify(n => n.SendInAppAsync(
            VerifiedCustomerId,
            It.IsAny<string>(),
            It.Is<string>(msg => msg.Contains("loan") || msg.Contains("Loan")),
            It.IsAny<string>(),
            It.IsAny<int?>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════
    // RECORD REPAYMENT TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task RecordRepayment_InterestFirstAllocation_CorrectSplit()
    {
        // Arrange: Loan of ₹50,000 at 2%/month, active for 3 months
        // Expected interest: ₹50,000 × 2% × 3 = ₹3,000
        // Payment of ₹10,000 → ₹3,000 to interest, ₹7,000 to principal
        var loanId = 1;
        var loan   = BuildActiveLoan(loanId, principal: 50_000m, monthsOld: 3);

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        var calcResult = new InterestCalculationResult(
            RegularInterest: 3_000m, PenaltyInterest: 0m, TotalInterest: 3_000m,
            TotalOutstanding: 53_000m, TotalDays: 90, IsOverdue: false, DaysOverdue: 0,
            CalculationBreakdown: "Test breakdown");

        _calcMock.Setup(c => c.Calculate(It.IsAny<InterestCalculationInput>()))
            .Returns(calcResult);

        var dto = new RecordRepaymentDto(loanId, DateOnly.FromDateTime(DateTime.Today), 10_000m, "Cash", null);

        // Act
        var result = await _sut.RecordRepaymentAsync(dto, AdminUserId);

        // Assert
        result.Should().NotBeNull();
        result.AmountPaid.Should().Be(10_000m);
        result.InterestComponent.Should().Be(3_000m);
        result.PrincipalComponent.Should().Be(7_000m);
        result.PenaltyAmount.Should().Be(0m);
        result.PrincipalBalanceAfter.Should().Be(43_000m); // 50,000 - 7,000
        result.ReceiptNumber.Should().StartWith("RCP-");
    }

    [Fact]
    public async Task RecordRepayment_PenaltyAllocatedFirst()
    {
        // Arrange: overdue loan with penalty
        var loanId = 2;
        var loan   = BuildOverdueLoan(loanId, principal: 20_000m, daysOverdue: 45);

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        // 12 months regular interest: ₹4,800 + 1.5 months penalty: ₹300
        var calcResult = new InterestCalculationResult(
            RegularInterest: 4_800m, PenaltyInterest: 300m, TotalInterest: 5_100m,
            TotalOutstanding: 25_100m, TotalDays: 410, IsOverdue: true, DaysOverdue: 45,
            CalculationBreakdown: "Overdue test");

        _calcMock.Setup(c => c.Calculate(It.IsAny<InterestCalculationInput>()))
            .Returns(calcResult);

        var dto = new RecordRepaymentDto(loanId, DateOnly.FromDateTime(DateTime.Today), 2_000m, "Cash", null);

        // Act
        var result = await _sut.RecordRepaymentAsync(dto, AdminUserId);

        // Assert: penalty cleared first (₹300), then interest (₹1,700), principal (₹0)
        result.PenaltyAmount.Should().Be(300m);
        result.InterestComponent.Should().Be(1_700m);
        result.PrincipalComponent.Should().Be(0m);
    }

    [Fact]
    public async Task RecordRepayment_FullRepayment_ClosesLoan()
    {
        // Arrange
        var loanId = 3;
        var loan   = BuildActiveLoan(loanId, principal: 10_000m, monthsOld: 1);

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        var calcResult = new InterestCalculationResult(
            RegularInterest: 200m, PenaltyInterest: 0m, TotalInterest: 200m,
            TotalOutstanding: 10_200m, TotalDays: 30, IsOverdue: false, DaysOverdue: 0,
            CalculationBreakdown: "Full payoff");

        _calcMock.Setup(c => c.Calculate(It.IsAny<InterestCalculationInput>()))
            .Returns(calcResult);

        var dto = new RecordRepaymentDto(loanId, DateOnly.FromDateTime(DateTime.Today), 10_200m, "UPI", null);

        // Act
        var result = await _sut.RecordRepaymentAsync(dto, AdminUserId);

        // Assert
        result.PrincipalBalanceAfter.Should().BeLessThanOrEqualTo(0.01m);
        loan.LoanStatus.Should().Be(LoanStatus.Closed);
        loan.ClosedDate.Should().NotBeNull();

        // Verify customer notification about gold return
        _notifMock.Verify(n => n.SendInAppAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Closed") || s.Contains("closed")),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RecordRepayment_Overpayment_ThrowsInvalidOperationException()
    {
        var loanId = 4;
        var loan   = BuildActiveLoan(loanId, principal: 5_000m, monthsOld: 1);

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        var calcResult = new InterestCalculationResult(
            RegularInterest: 100m, PenaltyInterest: 0m, TotalInterest: 100m,
            TotalOutstanding: 5_100m, TotalDays: 30, IsOverdue: false, DaysOverdue: 0,
            CalculationBreakdown: "");

        _calcMock.Setup(c => c.Calculate(It.IsAny<InterestCalculationInput>()))
            .Returns(calcResult);

        // Pay ₹6,000 against ₹5,100 outstanding (₹900 overpayment)
        var dto = new RecordRepaymentDto(loanId, DateOnly.FromDateTime(DateTime.Today), 6_000m, "Cash", null);

        var act = async () => await _sut.RecordRepaymentAsync(dto, AdminUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds outstanding*");
    }

    [Fact]
    public async Task RecordRepayment_ZeroAmount_ThrowsInvalidOperationException()
    {
        var loanId = 5;
        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(BuildActiveLoan(loanId, 10_000m, 1));

        var dto = new RecordRepaymentDto(loanId, DateOnly.FromDateTime(DateTime.Today), 0m, "Cash", null);

        var act = async () => await _sut.RecordRepaymentAsync(dto, AdminUserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public async Task RecordRepayment_ClosedLoan_ThrowsInvalidOperationException()
    {
        var loanId = 6;
        var loan = BuildActiveLoan(loanId, 10_000m, 1);
        loan.LoanStatus = LoanStatus.Closed;

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        var dto = new RecordRepaymentDto(loanId, DateOnly.FromDateTime(DateTime.Today), 1_000m, "Cash", null);

        var act = async () => await _sut.RecordRepaymentAsync(dto, AdminUserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Closed*");
    }

    // ═══════════════════════════════════════════════════════
    // CLOSE LOAN TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CloseLoan_ActiveLoan_ChangesStatusToClosed()
    {
        var loanId = 7;
        var loan   = BuildActiveLoan(loanId, 15_000m, 6);

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        var dto = new CloseLoanDto(loanId, "Customer repaid fully in-shop", GoldReturned: true);

        // Act
        await _sut.CloseLoanAsync(dto, AdminUserId);

        // Assert
        loan.LoanStatus.Should().Be(LoanStatus.Closed);
        loan.ClosedDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));

        // Notification sent
        _notifMock.Verify(n => n.SendInAppAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(s => s.Contains("closed") || s.Contains("Closed") || s.Contains("gold") || s.Contains("Gold")),
            It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task CloseLoan_AlreadyClosed_ThrowsInvalidOperationException()
    {
        var loanId = 8;
        var loan   = BuildActiveLoan(loanId, 10_000m, 3);
        loan.LoanStatus = LoanStatus.Closed;

        _loanRepoMock.Setup(r => r.GetByIdAsync(loanId, true, true))
            .ReturnsAsync(loan);

        var dto = new CloseLoanDto(loanId, "Test", true);

        var act = async () => await _sut.CloseLoanAsync(dto, AdminUserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already closed*");
    }

    // ═══════════════════════════════════════════════════════
    // DEFAULT CHECK TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAndMarkDefaultedLoans_90DaysOverdue_MarksAsDefaulted()
    {
        // Arrange: a loan that matured 95 days ago
        var loan = BuildActiveLoan(99, 30_000m, monthsOld: 16);
        loan.MaturityDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-95));
        loan.LoanStatus   = LoanStatus.Active;
        loan.Customer     = _db.Users.Find(VerifiedCustomerId);

        _loanRepoMock.Setup(r => r.GetLoansDueForDefaultCheckAsync())
            .ReturnsAsync(new List<GoldLoan> { loan });

        // Act
        await _sut.CheckAndMarkDefaultedLoansAsync();

        // Assert
        loan.LoanStatus.Should().Be(LoanStatus.Defaulted);

        // Admin notification
        _notifMock.Verify(n => n.NotifyAdminsAsync(
            It.Is<string>(s => s.Contains("Default") || s.Contains("default")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CheckAndMarkDefaultedLoans_88DaysOverdue_DoesNotMark()
    {
        // Arrange: 88 days — just under the 90-day threshold
        var loan = BuildActiveLoan(100, 30_000m, monthsOld: 15);
        loan.MaturityDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-88));
        loan.LoanStatus   = LoanStatus.Active;

        _loanRepoMock.Setup(r => r.GetLoansDueForDefaultCheckAsync())
            .ReturnsAsync(new List<GoldLoan> { loan });

        // Act
        await _sut.CheckAndMarkDefaultedLoansAsync();

        // Assert — still Active
        loan.LoanStatus.Should().Be(LoanStatus.Active);
    }

    [Fact]
    public async Task CheckAndMarkDefaultedLoans_NoOverdueLoans_NoChanges()
    {
        _loanRepoMock.Setup(r => r.GetLoansDueForDefaultCheckAsync())
            .ReturnsAsync(new List<GoldLoan>());

        await _sut.CheckAndMarkDefaultedLoansAsync();

        _notifMock.Verify(n => n.NotifyAdminsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════
    // ELIGIBILITY CHECK TESTS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CheckEligibility_KYCPending_NotEligible()
    {
        var request = new LoanEligibilityRequestDto(
            CustomerUserId: UnverifiedCustomerId,
            GoldItems: new List<CreateGoldLoanItemDto>
            {
                new("Gold ring", 20m, "22K", 13_000m, null, null, null)
            });

        var result = await _sut.CheckEligibilityAsync(request);

        result.IsEligible.Should().BeFalse();
        result.IneligibilityReason.Should().Contain("KYC");
    }

    [Fact]
    public async Task CheckEligibility_VerifiedCustomer_ReturnsMaxLoanAmount()
    {
        var request = new LoanEligibilityRequestDto(
            CustomerUserId: VerifiedCustomerId,
            GoldItems: new List<CreateGoldLoanItemDto>
            {
                new("Gold bangle", 50m, "22K", 32_500m, null, null, null)
            });

        // 50g × ₹650/g (22K rate from mock) = ₹32,500 × 75% = ₹24,375
        var result = await _sut.CheckEligibilityAsync(request);

        result.IsEligible.Should().BeTrue();
        result.MaxEligibleLoanAmount.Should().BeApproximately(24_375m, 50m);
        result.TotalGoldWeightGrams.Should().Be(50m);
        result.LoanToValuePercent.Should().Be(75m);
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════

    private void SetupLoanRepoForCreate()
    {
        _loanRepoMock.Setup(r => r.GetNextSequenceForYearAsync(It.IsAny<int>()))
            .ReturnsAsync(1);
        _loanRepoMock.Setup(r => r.AddAsync(It.IsAny<GoldLoan>()))
            .Returns(Task.CompletedTask);
        _loanRepoMock.Setup(r => r.SaveAsync())
            .Returns(Task.CompletedTask);
        _notifMock.Setup(n => n.SendInAppAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int?>())).Returns(Task.CompletedTask);
        _notifMock.Setup(n => n.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>())).Returns(Task.CompletedTask);
    }

    private static CreateGoldLoanDto BuildCreateLoanDto(
        string customerId, decimal principalAmount, decimal goldWeightGrams = 50m) =>
        new(
            CustomerUserId:  customerId,
            LoanDate:        DateOnly.FromDateTime(DateTime.Today),
            PrincipalAmount: principalAmount,
            GoldPurity:      "22K",
            GoldItems: new List<CreateGoldLoanItemDto>
            {
                new("Gold chain", goldWeightGrams, "22K",
                    goldWeightGrams * 650m, null, null, null)
            },
            Notes: null);

    private static GoldLoan BuildActiveLoan(int id, decimal principal, int monthsOld)
    {
        var loanDate    = DateOnly.FromDateTime(DateTime.Today.AddMonths(-monthsOld));
        var maturityDate = loanDate.AddMonths(12);
        return new GoldLoan
        {
            Id                       = id,
            LoanNumber               = $"GL-{DateTime.Today.Year}-{id:D4}",
            CustomerUserId           = VerifiedCustomerId,
            CreatedByUserId          = AdminUserId,
            InterestSettingId        = 1,
            LoanDate                 = loanDate,
            MaturityDate             = maturityDate,
            PrincipalAmount          = principal,
            GoldDepositedWeightGrams = 50m,
            GoldPurity               = "22K",
            GoldCurrentValueAtDeposit = principal / 0.75m,
            LoanToValuePercent       = 75m,
            LoanStatus               = LoanStatus.Active,
            TotalRepaid              = 0m,
            InterestSetting = new InterestSetting
            {
                InterestRatePercent = 2m,
                CalculationType     = InterestCalculationType.Monthly,
                CompoundingEnabled  = false,
                PenaltyRatePercent  = 1m,
                DefaultTenureMonths = 12,
                LoanToValuePercent  = 75m,
                EffectiveFrom       = DateOnly.FromDateTime(DateTime.Today.AddYears(-1))
            },
            Repayments = new List<LoanRepayment>(),
            GoldItems  = new List<GoldLoanItem>
            {
                new() { Id = id * 10, GoldLoanId = id, ItemDescription = "Gold chain",
                        WeightGrams = 50m, Purity = "22K", EstimatedValue = principal / 0.75m }
            },
            Customer = new ApplicationUser
            {
                Id = VerifiedCustomerId, FirstName = "Priya", LastName = "Sharma",
                Email = "priya@test.com", PhoneNumber = "9876543210"
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    private static GoldLoan BuildOverdueLoan(int id, decimal principal, int daysOverdue)
    {
        var loan = BuildActiveLoan(id, principal, monthsOld: 13);
        loan.MaturityDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysOverdue));
        loan.LoanStatus   = LoanStatus.Active;
        return loan;
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}

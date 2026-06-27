using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShreeJewellers.Application.DTOs.GoldLoan;
using ShreeJewellers.Infrastructure.Services;
using System.Security.Claims;

namespace ShreeJewellers.API.Controllers;

/// <summary>
/// Gold Loan lifecycle management.
/// Handles creation, repayment recording, outstanding balance, schedule, and admin actions.
/// </summary>
[ApiController]
[Route("api/goldloan")]
[Authorize]
[Produces("application/json")]
public class GoldLoanController : ControllerBase
{
    private readonly IGoldLoanService _loanService;

    public GoldLoanController(IGoldLoanService loanService)
        => _loanService = loanService;

    // ─── POST /api/goldloan ──────────────────────────────────────────────

    /// <summary>
    /// Create a new gold loan. Validates KYC, calculates LTV, generates loan number.
    /// Admin/Staff only — customers cannot self-create loans.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    public async Task<IActionResult> CreateLoan([FromBody] CreateGoldLoanDto dto)
    {
        var createdBy = GetUserId();
        var result = await _loanService.CreateLoanAsync(dto, createdBy);
        return CreatedAtAction(nameof(GetLoanById), new { id = result.Id }, result);
    }

    // ─── POST /api/goldloan/eligibility ──────────────────────────────────

    /// <summary>
    /// Check loan eligibility before creation.
    /// Returns max eligible amount, LTV, current gold rate, proposed maturity date.
    /// </summary>
    [HttpPost("eligibility")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    public async Task<IActionResult> CheckEligibility([FromBody] LoanEligibilityRequestDto dto)
    {
        var result = await _loanService.CheckEligibilityAsync(dto);
        return Ok(result);
    }

    // ─── GET /api/goldloan/{id} ──────────────────────────────────────────

    /// <summary>
    /// Get full loan details including gold items and repayment history.
    /// Customers can only view their own loans.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLoanById(int id)
    {
        var loan = await _loanService.GetLoanAsync(id);

        // Resource-based: customers can only see their own loans
        if (IsCustomer() && loan.CustomerUserId != GetUserId())
            return Forbid();

        return Ok(loan);
    }

    // ─── GET /api/goldloan/customer/{customerId} ──────────────────────────

    /// <summary>
    /// Get all loans for a specific customer.
    /// Customers can only retrieve their own loans.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetCustomerLoans(string customerId)
    {
        if (IsCustomer() && customerId != GetUserId())
            return Forbid();

        var loans = await _loanService.GetCustomerLoansAsync(customerId);
        return Ok(loans);
    }

    // ─── GET /api/goldloan/active ─────────────────────────────────────────

    /// <summary>
    /// Paginated list of all active/partially-repaid/extended loans.
    /// Supports search by customer name, phone, or CustomerCode.
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    public async Task<IActionResult> GetActiveLoans(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var loans = await _loanService.GetActiveLoansAsync(page, pageSize, search);
        return Ok(new { page, pageSize, data = loans });
    }

    // ─── POST /api/goldloan/{id}/repayment ───────────────────────────────

    /// <summary>
    /// Record a cash repayment against a loan.
    /// Automatically allocates payment: penalty → interest → principal.
    /// Closes the loan if fully repaid.
    /// </summary>
    [HttpPost("{id:int}/repayment")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    public async Task<IActionResult> RecordRepayment(
        int id, [FromBody] RecordRepaymentDto dto)
    {
        if (dto.GoldLoanId != id)
            return BadRequest(new { message = "Loan ID in URL and body must match." });

        var createdBy = GetUserId();
        var result = await _loanService.RecordRepaymentAsync(dto, createdBy);
        return Ok(result);
    }

    // ─── GET /api/goldloan/{id}/repayments ───────────────────────────────

    /// <summary>
    /// Get the full repayment history for a loan (all receipts, amounts, allocation breakdown).
    /// </summary>
    [HttpGet("{id:int}/repayments")]
    public async Task<IActionResult> GetRepayments(int id)
    {
        var loan = await _loanService.GetLoanAsync(id);

        if (IsCustomer() && loan.CustomerUserId != GetUserId())
            return Forbid();

        return Ok(loan.Repayments);
    }

    // ─── GET /api/goldloan/{id}/outstanding-balance ───────────────────────

    /// <summary>
    /// Get the current outstanding balance breakdown:
    /// principal remaining, accrued interest, penalty, total payoff amount.
    /// Pass ?asOf=yyyy-MM-dd to calculate balance as of a specific past/future date.
    /// </summary>
    [HttpGet("{id:int}/outstanding-balance")]
    public async Task<IActionResult> GetOutstandingBalance(
        int id, [FromQuery] DateOnly? asOf = null)
    {
        var loan = await _loanService.GetLoanAsync(id);

        if (IsCustomer() && loan.CustomerUserId != GetUserId())
            return Forbid();

        var balance = await _loanService.GetOutstandingBalanceAsync(id, asOf);
        return Ok(balance);
    }

    // ─── GET /api/goldloan/{id}/repayment-schedule ───────────────────────

    /// <summary>
    /// Returns the projected period-by-period repayment schedule for the loan,
    /// showing interest per period, cumulative balance, and actual payments made.
    /// </summary>
    [HttpGet("{id:int}/repayment-schedule")]
    public async Task<IActionResult> GetRepaymentSchedule(int id)
    {
        var loan = await _loanService.GetLoanAsync(id);

        if (IsCustomer() && loan.CustomerUserId != GetUserId())
            return Forbid();

        var schedule = await _loanService.GenerateRepaymentScheduleAsync(id);
        return Ok(schedule);
    }

    // ─── POST /api/goldloan/{id}/extend ──────────────────────────────────

    /// <summary>
    /// Extend the loan maturity date. Admin only. Logged in audit trail.
    /// </summary>
    [HttpPost("{id:int}/extend")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> ExtendLoan(int id, [FromBody] ExtendLoanDto dto)
    {
        if (dto.LoanId != id)
            return BadRequest(new { message = "Loan ID in URL and body must match." });

        var adminId = GetUserId();
        await _loanService.ExtendLoanAsync(dto, adminId);
        return Ok(new { message = "Loan maturity date extended successfully." });
    }

    // ─── POST /api/goldloan/{id}/close ───────────────────────────────────

    /// <summary>
    /// Manually close a loan (admin override). Records gold return status.
    /// </summary>
    [HttpPost("{id:int}/close")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> CloseLoan(int id, [FromBody] CloseLoanDto dto)
    {
        if (dto.LoanId != id)
            return BadRequest(new { message = "Loan ID in URL and body must match." });

        var adminId = GetUserId();
        await _loanService.CloseLoanAsync(dto, adminId);
        return Ok(new { message = "Loan closed successfully." });
    }

    // ─── POST /api/goldloan/{id}/auction ─────────────────────────────────

    /// <summary>
    /// Record auction of pledged gold for a defaulted loan.
    /// Computes proceeds vs outstanding and records profit/loss.
    /// </summary>
    [HttpPost("{id:int}/auction")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> AuctionGold(int id, [FromBody] AuctionGoldDto dto)
    {
        if (dto.LoanId != id)
            return BadRequest(new { message = "Loan ID in URL and body must match." });

        var adminId = GetUserId();
        await _loanService.ProcessAuctionAsync(dto, adminId);
        return Ok(new { message = "Auction recorded. Loan closed." });
    }

    // ─── GET /api/goldloan/defaulted ─────────────────────────────────────

    /// <summary>
    /// List all defaulted loans (>90 days past maturity with no activity).
    /// </summary>
    [HttpGet("defaulted")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetDefaultedLoans()
    {
        var loans = await _loanService.GetDefaultedLoansAsync();
        return Ok(loans);
    }

    // ─── GET /api/goldloan/due-this-week ─────────────────────────────────

    /// <summary>
    /// Loans maturing within the next 7 days — used for staff reminders and notifications.
    /// </summary>
    [HttpGet("due-this-week")]
    [Authorize(Roles = "SuperAdmin,Admin,Staff")]
    public async Task<IActionResult> GetLoansDueThisWeek()
    {
        var loans = await _loanService.GetLoansDueThisWeekAsync();
        return Ok(loans);
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    private string GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User identity not found.");

    private bool IsCustomer()
        => User.IsInRole("Customer") &&
           !User.IsInRole("Admin") &&
           !User.IsInRole("SuperAdmin") &&
           !User.IsInRole("Staff");
}

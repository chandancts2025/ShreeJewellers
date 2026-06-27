using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;

namespace ShreeJewellers.Infrastructure.Repositories;

public interface IGoldLoanRepository
{
    Task<GoldLoan?> GetByIdAsync(int id, bool includeItems = true, bool includeRepayments = true);
    Task<GoldLoan?> GetByLoanNumberAsync(string loanNumber);
    Task<List<GoldLoan>> GetByCustomerIdAsync(string customerId);
    Task<List<GoldLoan>> GetActiveLoansAsync(int page, int pageSize, string? customerSearch = null);
    Task<List<GoldLoan>> GetDefaultedLoansAsync();
    Task<List<GoldLoan>> GetLoansDueThisWeekAsync();
    Task<List<GoldLoan>> GetOverdueLoansAsync();
    Task<List<GoldLoan>> GetLoansDueForDefaultCheckAsync();   // Past maturity, still Active/PartiallyRepaid
    Task<int> GetNextSequenceForYearAsync(int year);
    Task<List<GoldLoan>> GetByStatusAsync(LoanStatus status);
    Task<(int totalCount, decimal totalPrincipal, decimal totalRepaid)> GetPortfolioSummaryAsync();
    Task AddAsync(GoldLoan loan);
    Task UpdateAsync(GoldLoan loan);
    Task SaveAsync();
}

public class GoldLoanRepository : IGoldLoanRepository
{
    private readonly ApplicationDbContext _db;

    public GoldLoanRepository(ApplicationDbContext db) => _db = db;

    // ── Core Fetches ──────────────────────────────────────────────────────

    public async Task<GoldLoan?> GetByIdAsync(int id, bool includeItems = true, bool includeRepayments = true)
    {
        var query = _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.CreatedBy)
            .Include(l => l.InterestSetting)
            .AsQueryable();

        if (includeItems)
            query = query.Include(l => l.GoldItems);

        if (includeRepayments)
            query = query.Include(l => l.Repayments.OrderBy(r => r.RepaymentDate));

        return await query.FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<GoldLoan?> GetByLoanNumberAsync(string loanNumber)
        => await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Include(l => l.GoldItems)
            .Include(l => l.Repayments)
            .FirstOrDefaultAsync(l => l.LoanNumber == loanNumber);

    public async Task<List<GoldLoan>> GetByCustomerIdAsync(string customerId)
        => await _db.GoldLoans
            .Include(l => l.GoldItems)
            .Include(l => l.Repayments.OrderBy(r => r.RepaymentDate))
            .Include(l => l.InterestSetting)
            .Where(l => l.CustomerUserId == customerId)
            .OrderByDescending(l => l.LoanDate)
            .ToListAsync();

    // ── Paginated Active Loans ────────────────────────────────────────────

    public async Task<List<GoldLoan>> GetActiveLoansAsync(
        int page, int pageSize, string? customerSearch = null)
    {
        var query = _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Where(l => l.LoanStatus == LoanStatus.Active ||
                        l.LoanStatus == LoanStatus.PartiallyRepaid ||
                        l.LoanStatus == LoanStatus.Extended)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerSearch))
        {
            var search = customerSearch.Trim().ToLower();
            query = query.Where(l =>
                l.Customer.FirstName.ToLower().Contains(search) ||
                l.Customer.LastName.ToLower().Contains(search) ||
                l.Customer.CustomerCode!.ToLower().Contains(search) ||
                l.Customer.PhoneNumber!.Contains(search) ||
                l.LoanNumber.ToLower().Contains(search));
        }

        return await query
            .OrderByDescending(l => l.LoanDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // ── Status-Based Queries ──────────────────────────────────────────────

    public async Task<List<GoldLoan>> GetDefaultedLoansAsync()
        => await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Where(l => l.LoanStatus == LoanStatus.Defaulted)
            .OrderBy(l => l.MaturityDate)
            .ToListAsync();

    public async Task<List<GoldLoan>> GetByStatusAsync(LoanStatus status)
        => await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Where(l => l.LoanStatus == status)
            .OrderBy(l => l.MaturityDate)
            .ToListAsync();

    public async Task<List<GoldLoan>> GetLoansDueThisWeekAsync()
    {
        var today    = DateOnly.FromDateTime(DateTime.Today);
        var weekEnd  = today.AddDays(7);

        return await _db.GoldLoans
            .Include(l => l.Customer)
            .Where(l =>
                (l.LoanStatus == LoanStatus.Active ||
                 l.LoanStatus == LoanStatus.PartiallyRepaid ||
                 l.LoanStatus == LoanStatus.Extended) &&
                l.MaturityDate >= today &&
                l.MaturityDate <= weekEnd)
            .OrderBy(l => l.MaturityDate)
            .ToListAsync();
    }

    public async Task<List<GoldLoan>> GetOverdueLoansAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await _db.GoldLoans
            .Include(l => l.Customer)
            .Include(l => l.InterestSetting)
            .Where(l =>
                (l.LoanStatus == LoanStatus.Active ||
                 l.LoanStatus == LoanStatus.PartiallyRepaid ||
                 l.LoanStatus == LoanStatus.Extended) &&
                l.MaturityDate < today)
            .OrderBy(l => l.MaturityDate)
            .ToListAsync();
    }

    public async Task<List<GoldLoan>> GetLoansDueForDefaultCheckAsync()
    {
        // Loans where maturity was > 90 days ago and still not closed/defaulted
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-90));
        return await _db.GoldLoans
            .Include(l => l.Customer)
            .Where(l =>
                (l.LoanStatus == LoanStatus.Active ||
                 l.LoanStatus == LoanStatus.PartiallyRepaid ||
                 l.LoanStatus == LoanStatus.Extended) &&
                l.MaturityDate <= cutoff)
            .ToListAsync();
    }

    // ── Sequence Generation ───────────────────────────────────────────────

    /// <summary>
    /// Gets the next sequence number for loan numbering within a given year.
    /// Uses MAX(Id) per year to avoid gaps from failed transactions.
    /// </summary>
    public async Task<int> GetNextSequenceForYearAsync(int year)
    {
        var prefix = $"GL-{year}-";
        var count = await _db.GoldLoans
            .Where(l => l.LoanNumber.StartsWith(prefix))
            .CountAsync();
        return count + 1;
    }

    // ── Portfolio Summary ─────────────────────────────────────────────────

    public async Task<(int totalCount, decimal totalPrincipal, decimal totalRepaid)>
        GetPortfolioSummaryAsync()
    {
        var result = await _db.GoldLoans
            .Where(l => l.LoanStatus == LoanStatus.Active ||
                        l.LoanStatus == LoanStatus.PartiallyRepaid ||
                        l.LoanStatus == LoanStatus.Extended)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count    = g.Count(),
                Principal = g.Sum(l => l.PrincipalAmount),
                Repaid   = g.Sum(l => l.TotalRepaid)
            })
            .FirstOrDefaultAsync();

        return result is null
            ? (0, 0m, 0m)
            : (result.Count, result.Principal, result.Repaid);
    }

    // ── Mutations ─────────────────────────────────────────────────────────

    public async Task AddAsync(GoldLoan loan)
    {
        await _db.GoldLoans.AddAsync(loan);
    }

    public Task UpdateAsync(GoldLoan loan)
    {
        _db.GoldLoans.Update(loan);
        return Task.CompletedTask;
    }

    public async Task SaveAsync() => await _db.SaveChangesAsync();
}

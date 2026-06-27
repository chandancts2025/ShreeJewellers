using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShreeJewellers.Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted background service that runs once per day.
/// Responsibilities:
///   1. Auto-mark loans as Defaulted if 90+ days past maturity
///   2. Send due-date reminders (7 days, 3 days, on due date)
///   3. Send overdue reminders to customers with outstanding loans
///
/// Registered in Program.cs as: builder.Services.AddHostedService<LoanStatusCheckerService>()
/// Runs at a configurable time (default: 09:00 IST daily).
/// </summary>
public class LoanStatusCheckerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoanStatusCheckerService> _logger;

    // Run at 09:00 AM IST every day
    private static readonly TimeSpan RunAt = new(3, 30, 0);  // 03:30 UTC = 09:00 IST

    public LoanStatusCheckerService(
        IServiceScopeFactory scopeFactory,
        ILogger<LoanStatusCheckerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LoanStatusCheckerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun();
            _logger.LogInformation(
                "LoanStatusChecker: next run in {Delay}", delay);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            await RunJobAsync(stoppingToken);
        }

        _logger.LogInformation("LoanStatusCheckerService stopped.");
    }

    private async Task RunJobAsync(CancellationToken ct)
    {
        _logger.LogInformation("LoanStatusChecker: job started at {Time}", DateTime.UtcNow);

        using var scope          = _scopeFactory.CreateScope();
        var loanService          = scope.ServiceProvider.GetRequiredService<Services.IGoldLoanService>();
        var notificationService  = scope.ServiceProvider.GetRequiredService<Services.INotificationService>();
        var loanRepo             = scope.ServiceProvider.GetRequiredService<Repositories.IGoldLoanRepository>();

        try
        {
            // ── 1. Mark defaulted loans ───────────────────────────────────
            await loanService.CheckAndMarkDefaultedLoansAsync();

            // ── 2. Send due-date reminders ────────────────────────────────
            await SendDueDateRemindersAsync(loanRepo, notificationService);

            // ── 3. Send overdue reminders ─────────────────────────────────
            await SendOverdueRemindersAsync(loanRepo, notificationService, loanService);

            _logger.LogInformation("LoanStatusChecker: job completed at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Never let a job crash kill the service — log and continue
            _logger.LogError(ex, "LoanStatusChecker: job failed.");
        }
    }

    // ── Due Date Reminders ────────────────────────────────────────────────

    private async Task SendDueDateRemindersAsync(
        Repositories.IGoldLoanRepository loanRepo,
        Services.INotificationService notificationService)
    {
        var today     = DateOnly.FromDateTime(DateTime.Today);
        var dueLoans  = await loanRepo.GetLoansDueThisWeekAsync();

        foreach (var loan in dueLoans)
        {
            var daysUntilDue = loan.MaturityDate.DayNumber - today.DayNumber;

            // Only send reminders on: 7 days, 3 days, 1 day, and on due date
            if (daysUntilDue is not (7 or 3 or 1 or 0)) continue;

            var message = daysUntilDue == 0
                ? $"Your gold loan {loan.LoanNumber} is due TODAY. " +
                  $"Please visit the shop to repay or request an extension."
                : $"Reminder: Your gold loan {loan.LoanNumber} is due in {daysUntilDue} day(s) " +
                  $"on {loan.MaturityDate:dd-MMM-yyyy}. " +
                  $"Please arrange repayment to avoid penalty charges.";

            await notificationService.SendInAppAsync(
                loan.CustomerUserId,
                $"Loan Due {(daysUntilDue == 0 ? "Today" : $"in {daysUntilDue} Days")} — {loan.LoanNumber}",
                message,
                "GoldLoan", loan.Id);

            // Also send SMS for due-today and 1-day reminders
            if (daysUntilDue is 0 or 1 && loan.Customer?.PhoneNumber is not null)
            {
                await notificationService.SendSmsAsync(
                    loan.Customer.PhoneNumber,
                    $"Shree Jewellers: Gold loan {loan.LoanNumber} is due " +
                    $"{(daysUntilDue == 0 ? "TODAY" : "TOMORROW")}. " +
                    $"Avoid penalty — repay now. Call: +91-22-12345678");
            }

            _logger.LogInformation(
                "Due reminder sent for loan {LoanNumber}, {Days} days until due.",
                loan.LoanNumber, daysUntilDue);
        }
    }

    // ── Overdue Reminders ─────────────────────────────────────────────────

    private async Task SendOverdueRemindersAsync(
        Repositories.IGoldLoanRepository loanRepo,
        Services.INotificationService notificationService,
        Services.IGoldLoanService loanService)
    {
        var overdueLoans = await loanRepo.GetOverdueLoansAsync();
        var today        = DateOnly.FromDateTime(DateTime.Today);

        foreach (var loan in overdueLoans)
        {
            var daysOverdue = today.DayNumber - loan.MaturityDate.DayNumber;

            // Send reminders at: 1, 7, 15, 30, 60, 75 days overdue
            // (avoid spamming every day)
            if (daysOverdue is not (1 or 7 or 15 or 30 or 60 or 75)) continue;

            var balance = await loanService.GetOutstandingBalanceAsync(loan.Id, today);

            var urgency = daysOverdue >= 75
                ? "⚠️ URGENT: Your gold may be auctioned soon. "
                : daysOverdue >= 30
                    ? "Please repay immediately to prevent further penalty. "
                    : "";

            var message = $"{urgency}Your gold loan {loan.LoanNumber} is {daysOverdue} days overdue. " +
                          $"Outstanding balance: ₹{balance.TotalOutstanding:N2} " +
                          $"(includes ₹{balance.PenaltyInterest:N2} penalty). " +
                          $"Please visit the shop or contact us to resolve.";

            await notificationService.SendInAppAsync(
                loan.CustomerUserId,
                $"Overdue Notice — {loan.LoanNumber} ({daysOverdue} days)",
                message, "GoldLoan", loan.Id);

            if (loan.Customer?.PhoneNumber is not null)
            {
                await notificationService.SendSmsAsync(
                    loan.Customer.PhoneNumber,
                    $"Shree Jewellers OVERDUE NOTICE: Loan {loan.LoanNumber} is {daysOverdue} days " +
                    $"overdue. Balance: Rs.{balance.TotalOutstanding:N0}. " +
                    $"Call +91-22-12345678 immediately.");
            }

            // Admin notification at 75 days (pre-auction warning)
            if (daysOverdue == 75)
            {
                await notificationService.NotifyAdminsAsync(
                    $"Pre-Auction Warning: {loan.LoanNumber}",
                    $"Gold loan {loan.LoanNumber} for {loan.Customer?.FullName} is {daysOverdue} days overdue. " +
                    $"Outstanding: ₹{balance.TotalOutstanding:N2}. " +
                    $"Auto-default will trigger in 15 days.");
            }

            _logger.LogWarning(
                "Overdue reminder sent for {LoanNumber}, {Days} days overdue, balance ₹{Balance}",
                loan.LoanNumber, daysOverdue, balance.TotalOutstanding);
        }
    }

    // ── Scheduling ────────────────────────────────────────────────────────

    private static TimeSpan CalculateDelayUntilNextRun()
    {
        var now     = DateTime.UtcNow;
        var today   = now.Date;
        var runTime = today + RunAt;

        if (runTime <= now)
            runTime = runTime.AddDays(1);

        return runTime - now;
    }
}

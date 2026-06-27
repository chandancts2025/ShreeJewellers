using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;

namespace ShreeJewellers.Infrastructure.Services;

public interface INotificationService
{
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false);
    Task SendSmsAsync(string phoneNumber, string message);
    Task SendInAppAsync(string userId, string subject, string body,
        string? relatedEntityType = null, int? relatedEntityId = null);
    Task NotifyAdminsAsync(string subject, string body);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db, IConfiguration config, ILogger<NotificationService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    // ── Email ─────────────────────────────────────────────────────────────

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        try
        {
            var emailConfig = _config.GetSection("EmailSettings");
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(emailConfig["SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = isHtml
                ? new BodyBuilder { HtmlBody = body }.ToMessageBody()
                : new BodyBuilder { TextBody = body }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                emailConfig["SmtpHost"],
                int.Parse(emailConfig["SmtpPort"] ?? "587"),
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(emailConfig["SenderEmail"], emailConfig["SenderPassword"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            // Do not rethrow — email failure should not break the main transaction
        }
    }

    // ── SMS via Twilio ────────────────────────────────────────────────────

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        // Integrate Twilio SDK here
        // Twilio.Rest.Api.V2010.Account.MessageResource.CreateAsync(...)
        _logger.LogInformation("SMS to {Phone}: {Message}", MaskPhone(phoneNumber), message);
        await Task.CompletedTask;
    }

    // ── In-App Notification ───────────────────────────────────────────────

    public async Task SendInAppAsync(string userId, string subject, string body,
        string? relatedEntityType = null, int? relatedEntityId = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId            = userId,
            Type              = NotificationType.InApp,
            Subject           = subject,
            Body              = body,
            SentAt            = DateTime.UtcNow,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId   = relatedEntityId,
            CreatedAt         = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // ── Notify All Admins ─────────────────────────────────────────────────

    public async Task NotifyAdminsAsync(string subject, string body)
    {
        // Find all users in Admin / SuperAdmin roles
        var adminRoleIds = await _db.Roles
            .Where(r => r.Name == "Admin" || r.Name == "SuperAdmin")
            .Select(r => r.Id)
            .ToListAsync();

        var adminUserIds = await _db.UserRoles
            .Where(ur => adminRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .ToListAsync();

        var notifications = adminUserIds.Select(uid => new Notification
        {
            UserId    = uid,
            Type      = NotificationType.InApp,
            Subject   = subject,
            Body      = body,
            SentAt    = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();
    }

    private static string MaskPhone(string phone) =>
        phone.Length >= 4 ? "XXXXXX" + phone[^4..] : "XXXX";
}

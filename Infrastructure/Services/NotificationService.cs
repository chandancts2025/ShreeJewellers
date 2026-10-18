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
    Task<(bool Success, string Message)> SendEmailWithStatusAsync(string toEmail, string subject, string body, bool isHtml = false);
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
        await SendEmailWithStatusAsync(toEmail, subject, body, isHtml);
    }

    public async Task<(bool Success, string Message)> SendEmailWithStatusAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email recipient was empty; skipping email.");
            return (false, "Recipient email address is required.");
        }

        try
        {
            var smtpConfig = _config.GetSection("SmtpSettings");
            var emailConfig = _config.GetSection("EmailSettings");

            var host = smtpConfig["Host"] ?? emailConfig["SmtpHost"] ?? "smtp.gmail.com";
            var portStr = smtpConfig["Port"] ?? emailConfig["SmtpPort"] ?? "587";
            var port = int.TryParse(portStr, out var p) ? p : 587;
            var userName = smtpConfig["UserName"] ?? emailConfig["SenderEmail"] ?? smtpConfig["FromEmail"];
            var password = smtpConfig["Password"] ?? emailConfig["SenderPassword"];
            var fromEmail = smtpConfig["FromEmail"] ?? emailConfig["SenderEmail"] ?? userName ?? "chandankr.mandal87@gmail.com";
            var fromName = smtpConfig["FromName"] ?? emailConfig["SenderName"] ?? "EduManage Pro Academy";
            var enableSsl = bool.TryParse(smtpConfig["EnableSsl"] ?? emailConfig["EnableSsl"], out var ssl) ? ssl : true;
            var devMode = bool.TryParse(smtpConfig["DevMode"] ?? emailConfig["DevMode"], out var dev) && dev;

            if (devMode)
            {
                _logger.LogInformation("[DevMode] Suppressed sending email to {Email}: {Subject}", toEmail, subject);
                return (true, "DevMode is enabled; email delivery simulated successfully.");
            }

            if (string.IsNullOrWhiteSpace(password) || password.StartsWith("SET_IN_ENV", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Email sending skipped: SMTP password is not configured.");
                return (false, "SMTP password is not configured.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
            message.Subject = subject;

            var builder = new BodyBuilder();
            if (isHtml || body.Contains("<html", StringComparison.OrdinalIgnoreCase) || body.Contains("<div", StringComparison.OrdinalIgnoreCase))
            {
                builder.HtmlBody = body;
                builder.TextBody = System.Text.RegularExpressions.Regex.Replace(body, "<.*?>", string.Empty);
            }
            else
            {
                builder.TextBody = body;
                builder.HtmlBody = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; background-color: #ffffff; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);"">
  <div style=""background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 24px; text-align: center; border-bottom: 3px solid #d4af37;"">
    <h2 style=""color: #d4af37; margin: 0; font-size: 22px; font-weight: 600; letter-spacing: 0.5px;"">{System.Net.WebUtility.HtmlEncode(fromName)}</h2>
    <p style=""color: #94a3b8; margin: 4px 0 0; font-size: 13px;"">Jewellery & Gold Loan Management System</p>
  </div>
  <div style=""padding: 28px 24px; color: #334155; line-height: 1.6; font-size: 14px;"">
    <pre style=""font-family: inherit; white-space: pre-wrap; margin: 0; font-size: 14px; background: transparent; border: none; padding: 0;"">{System.Net.WebUtility.HtmlEncode(body)}</pre>
  </div>
  <div style=""background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;"">
    <p style=""margin: 0;"">This is an automated notification from <strong>{System.Net.WebUtility.HtmlEncode(fromName)}</strong>.</p>
    <p style=""margin: 4px 0 0;"">If you have inquiries, please reply to this email or visit our store.</p>
  </div>
</div>";
            }
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOptions = !enableSsl
                ? SecureSocketOptions.None
                : (port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);

            await client.ConnectAsync(host, port, socketOptions);

            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(userName, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            return (true, $"Email sent successfully to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return (false, ex.Message);
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

        try
        {
            var adminEmails = await _db.Users
                .Where(u => adminUserIds.Contains(u.Id) && u.Email != null && u.IsActive)
                .Select(u => u.Email!)
                .ToListAsync();

            foreach (var email in adminEmails.Distinct())
            {
                await SendEmailAsync(email, $"[Admin Alert] {subject}", body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deliver admin notification emails.");
        }
    }

    private static string MaskPhone(string phone) =>
        phone.Length >= 4 ? "XXXXXX" + phone[^4..] : "XXXX";
}

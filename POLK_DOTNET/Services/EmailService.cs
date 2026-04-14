using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public class EmailService
    {
        private readonly ApplicationDbContext _context;

        public EmailService(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, List<EmailAttachment>? attachments = null)
        {
            var smtpHost = await GetSettingAsync("Email:SmtpHost");
            var smtpPortStr = await GetSettingAsync("Email:SmtpPort");
            var smtpUsername = await GetSettingAsync("Email:SmtpUsername");
            var smtpPassword = await GetSettingAsync("Email:SmtpPassword");
            var fromEmail = await GetSettingAsync("Email:FromAddress");
            var fromName = await GetSettingAsync("Email:FromName");
            var enableSslStr = await GetSettingAsync("Email:EnableSsl");

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(fromEmail))
                return false;

            int.TryParse(smtpPortStr, out int smtpPort);
            if (smtpPort == 0) smtpPort = 587;

            bool enableSsl = enableSslStr?.ToLower() != "false";

            try
            {
                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName ?? "POLK"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                if (attachments != null)
                {
                    foreach (var att in attachments)
                    {
                        var stream = new MemoryStream(att.Content);
                        mailMessage.Attachments.Add(new Attachment(stream, att.FileName, att.ContentType));
                    }
                }

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class EmailAttachment
    {
        public string FileName { get; set; } = null!;
        public byte[] Content { get; set; } = null!;
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public static class EventEmailTemplates
    {
        public const string AccountsEmail = "accounts@polk-hft.co.za";

        public static async Task SendEventPaymentConfirmedAsync(EmailService email, EventRegistration reg, Event ev)
        {
            var title = ev?.Title ?? "Event";
            var methodText = string.IsNullOrWhiteSpace(reg.PaymentMethod) ? "" : $" (via {reg.PaymentMethod})";

            var registrantBody = $@"
                <h2>Payment Confirmed</h2>
                <p>Dear {reg.Name},</p>
                <p>We have received your payment of <strong>R{reg.AmountPaid:F2}</strong> for <strong>{title}</strong>.</p>
                <p><strong>Registration #:</strong> {reg.Id}</p>
                <p><strong>Status:</strong> Paid</p>
                <p>We look forward to seeing you at the event.</p>
                <br/>
                <p>Kind regards,<br/>Pretoria Oos Lug Geweer Klub</p>";

            await email.SendEmailAsync(
                reg.EmailAddress,
                $"Payment Confirmed - {title}",
                registrantBody);

            var accountsBody = $@"
                <h2>Event Payment Received - Registration #{reg.Id}</h2>
                <p><strong>Event:</strong> {title}</p>
                <p><strong>Registrant:</strong> {reg.Name} {reg.Surname}</p>
                <p><strong>Amount Paid:</strong> R{reg.AmountPaid:F2}</p>
                <p><strong>Status:</strong> Paid</p>
                <p>Payment has been confirmed{methodText}.</p>";

            var notifyEmail = string.IsNullOrWhiteSpace(ev?.NotificationEmail) ? AccountsEmail : ev.NotificationEmail;
            await email.SendEmailAsync(
                notifyEmail,
                $"Event Payment Received - Registration #{reg.Id}",
                accountsBody);
        }
    }
}

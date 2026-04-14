using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YocoWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<YocoWebhookController> _logger;
        private readonly EmailService _emailService;
        private readonly ApplicationPdfService _pdfService;

        public YocoWebhookController(ApplicationDbContext context, ILogger<YocoWebhookController> logger, EmailService emailService, ApplicationPdfService pdfService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _pdfService = pdfService;
        }

        [HttpPost]
        public async Task<IActionResult> HandleWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Yoco webhook received: {Body}", body);

            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(body);

                var type = payload.GetProperty("type").GetString();
                if (type != "payment.succeeded")
                    return Ok();

                var data = payload.GetProperty("payload");

                if (data.GetProperty("metadata").TryGetProperty("applicationId", out var appIdElement))
                {
                    if (int.TryParse(appIdElement.GetString(), out int applicationId))
                    {
                        var application = await _context.MembershipApplications
                            .Include(a => a.Members)
                            .FirstOrDefaultAsync(a => a.Id == applicationId);

                        if (application != null)
                        {
                            application.Status = "Paid";
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Membership application {Id} marked as Paid via Yoco webhook", applicationId);

                            await SendPaymentConfirmationEmailsAsync(application);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Yoco webhook");
            }

            return Ok();
        }

        private async Task SendPaymentConfirmationEmailsAsync(MembershipApplication application)
        {
            var primaryMember = application.Members.FirstOrDefault(m => m.IsPrimary);
            var applicantName = primaryMember != null ? $"{primaryMember.FirstName} {primaryMember.Surname}" : "Applicant";

            // Notify accounts
            var accountsBody = $@"
                <h2>Payment Received - Application #{application.Id}</h2>
                <p><strong>Applicant:</strong> {applicantName}</p>
                <p><strong>Membership Type:</strong> {application.MembershipType}</p>
                <p><strong>Amount Paid:</strong> R{application.TotalAmount:F2}</p>
                <p><strong>Status:</strong> {application.Status}</p>
                <p>Payment has been confirmed via Yoco.</p>";

            await _emailService.SendEmailAsync(
                "accounts@polk-hft.co.za",
                $"Payment Received - Application #{application.Id}",
                accountsBody);

            // Send confirmation with PDF to applicant
            if (primaryMember != null)
            {
                var pdfBytes = _pdfService.GenerateApplicationPdf(application);
                var attachments = new List<EmailAttachment>
                {
                    new EmailAttachment
                    {
                        FileName = $"MembershipApplication_{application.Id}.pdf",
                        Content = pdfBytes,
                        ContentType = "application/pdf"
                    }
                };

                var applicantBody = $@"
                    <h2>Payment Confirmed</h2>
                    <p>Dear {primaryMember.FirstName},</p>
                    <p>We have received your payment of <strong>R{application.TotalAmount:F2}</strong> for your membership application.</p>
                    <p><strong>Application #:</strong> {application.Id}</p>
                    <p><strong>Status:</strong> Paid</p>
                    <p>Please find your updated application attached as a PDF for your records.</p>
                    <p>Thank you for joining POLK!</p>
                    <br/>
                    <p>Kind regards,<br/>Pretoria Oos Lug Geweer Klub</p>";

                await _emailService.SendEmailAsync(
                    primaryMember.EmailAddress,
                    $"Payment Confirmed - Membership Application #{application.Id}",
                    applicantBody,
                    attachments);
            }
        }
    }
}

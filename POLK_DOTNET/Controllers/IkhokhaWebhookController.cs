using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Pages;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Controllers
{
    // Server-to-server callback iKhokha POSTs to after a payment resolves.
    //
    // externalTransactionID convention:
    //   APP-{applicationId}  → membership application — mark Paid, send confirmation emails + PDF
    //   EVT-{registrationId} → event registration   — mark Paid, send confirmation email
    //
    // Authentication: IK-SIGN header (HMAC-SHA256 over (callback path + raw body)
    // keyed with the iKhokha App Secret). We never trust the body alone.
    //
    // Note: iKhokha can't POST to localhost; this only fires in production. The
    // PaymentResult page is the primary success handler (redirect-based) and the
    // webhook is the safety net.
    [ApiController]
    [Route("api/ikhokha")]
    public class IkhokhaWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IkhokhaPaymentService _ikhokhaService;
        private readonly EmailService _emailService;
        private readonly ApplicationPdfService _pdfService;
        private readonly ILogger<IkhokhaWebhookController> _logger;

        public IkhokhaWebhookController(
            ApplicationDbContext context,
            IkhokhaPaymentService ikhokhaService,
            EmailService emailService,
            ApplicationPdfService pdfService,
            ILogger<IkhokhaWebhookController> logger)
        {
            _context = context;
            _ikhokhaService = ikhokhaService;
            _emailService = emailService;
            _pdfService = pdfService;
            _logger = logger;
        }

        [HttpPost("callback")]
        public async Task<IActionResult> Callback()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("iKhokha callback received — body length {Len}", rawBody.Length);

            var ikSign = Request.Headers["IK-SIGN"].ToString();
            var callbackPath = Request.Path.ToString();
            var signatureValid = await _ikhokhaService.VerifyWebhookSignatureAsync(callbackPath, rawBody, ikSign);

            if (!signatureValid)
            {
                _logger.LogWarning("iKhokha callback signature mismatch — ik-sign {Sign}", ikSign);
                return Unauthorized();
            }

            IkhokhaWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<IkhokhaWebhookPayload>(rawBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iKhokha callback — malformed JSON. Body: {Body}", rawBody);
                return BadRequest("Malformed JSON");
            }

            if (payload == null || string.IsNullOrEmpty(payload.ExternalTransactionID))
            {
                _logger.LogWarning("iKhokha callback missing externalTransactionID");
                return BadRequest("Missing externalTransactionID");
            }

            var paid = string.Equals(payload.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(payload.Status, "PAID", StringComparison.OrdinalIgnoreCase);

            if (!paid)
            {
                _logger.LogInformation("iKhokha callback — payment not successful, status {Status}, ext-txn {Ext}",
                    payload.Status, payload.ExternalTransactionID);
                return Ok();
            }

            if (payload.ExternalTransactionID.StartsWith("APP-", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(payload.ExternalTransactionID["APP-".Length..], out var applicationId))
                    await MarkApplicationPaidAsync(applicationId, payload);
                else
                    _logger.LogWarning("iKhokha callback — malformed APP id: {Ext}", payload.ExternalTransactionID);
            }
            else if (payload.ExternalTransactionID.StartsWith("EVT-", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(payload.ExternalTransactionID["EVT-".Length..], out var registrationId))
                    await MarkRegistrationPaidAsync(registrationId, payload);
                else
                    _logger.LogWarning("iKhokha callback — malformed EVT id: {Ext}", payload.ExternalTransactionID);
            }
            else
            {
                _logger.LogWarning("iKhokha callback — unrecognised externalTransactionID prefix: {Ext}", payload.ExternalTransactionID);
                return BadRequest("Unrecognised externalTransactionID prefix");
            }

            return Ok();
        }

        private async Task MarkApplicationPaidAsync(int applicationId, IkhokhaWebhookPayload payload)
        {
            var application = await _context.MembershipApplications
                .Include(a => a.Members)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
            {
                _logger.LogWarning("iKhokha callback — membership application {Id} not found", applicationId);
                return;
            }

            // Idempotency — PaymentResult page may have already processed the redirect.
            if (application.Status == "Paid")
            {
                _logger.LogInformation("iKhokha callback — application {Id} already Paid, skipping", applicationId);
                return;
            }

            application.Status = "Paid";
            await _context.SaveChangesAsync();
            _logger.LogInformation("iKhokha callback — application {Id} marked Paid (PaylinkID {Plid})", applicationId, payload.PaylinkID);

            await SendApplicationPaymentEmailsAsync(application);
        }

        private async Task MarkRegistrationPaidAsync(int registrationId, IkhokhaWebhookPayload payload)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.Event)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (reg == null)
            {
                _logger.LogWarning("iKhokha callback — event registration {Id} not found", registrationId);
                return;
            }

            if (reg.Status == "Paid")
            {
                _logger.LogInformation("iKhokha callback — registration {Id} already Paid, skipping", registrationId);
                return;
            }

            reg.Status = "Paid";
            if (reg.Event != null)
                reg.AmountPaid = RegisterEventModel.ComputeEffectiveFee(reg.Event, reg.ShootSelection, reg.RifleOwnership);
            await _context.SaveChangesAsync();
            _logger.LogInformation("iKhokha callback — registration {Id} marked Paid (PaylinkID {Plid})", registrationId, payload.PaylinkID);

            if (reg.Event != null)
                await EventEmailTemplates.SendEventPaymentConfirmedAsync(_emailService, reg, reg.Event);
        }

        private async Task SendApplicationPaymentEmailsAsync(MembershipApplication application)
        {
            var primaryMember = application.Members.FirstOrDefault(m => m.IsPrimary);
            var applicantName = primaryMember != null ? $"{primaryMember.FirstName} {primaryMember.Surname}" : "Applicant";

            var accountsBody = $@"
                <h2>Payment Received - Application #{application.Id}</h2>
                <p><strong>Applicant:</strong> {applicantName}</p>
                <p><strong>Membership Type:</strong> {application.MembershipType}</p>
                <p><strong>Amount Paid:</strong> R{application.TotalAmount:F2}</p>
                <p><strong>Status:</strong> {application.Status}</p>
                <p>Payment has been confirmed via iKhokha.</p>";

            await _emailService.SendEmailAsync(
                "accounts@polk-hft.co.za",
                $"Payment Received - Application #{application.Id}",
                accountsBody);

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

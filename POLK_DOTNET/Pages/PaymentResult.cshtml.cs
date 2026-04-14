using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages
{
    public class PaymentResultModel : PageModel
    {
        private const string AccountsEmail = "accounts@polk-hft.co.za";

        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly ApplicationPdfService _pdfService;

        public PaymentResultModel(ApplicationDbContext context, EmailService emailService, ApplicationPdfService pdfService)
        {
            _context = context;
            _emailService = emailService;
            _pdfService = pdfService;
        }

        [BindProperty(SupportsGet = true)]
        public string Status { get; set; } = "failed";

        [BindProperty(SupportsGet = true)]
        public int? ApplicationId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? RegistrationId { get; set; }

        public string? EventTitle { get; set; }

        public async Task OnGetAsync()
        {
            if (Status != "success") return;

            if (ApplicationId.HasValue)
            {
                var application = await _context.MembershipApplications
                    .Include(a => a.Members)
                    .FirstOrDefaultAsync(a => a.Id == ApplicationId.Value);

                if (application != null && application.Status != "Paid")
                {
                    application.Status = "Paid";
                    await _context.SaveChangesAsync();
                    await SendMembershipPaymentEmailsAsync(application);
                }
            }
            else if (RegistrationId.HasValue)
            {
                var reg = await _context.EventRegistrations
                    .Include(r => r.Event)
                    .FirstOrDefaultAsync(r => r.Id == RegistrationId.Value);

                if (reg != null)
                {
                    EventTitle = reg.Event?.Title;

                    if (reg.Status != "Paid")
                    {
                        reg.Status = "Paid";
                        if (reg.Event?.EntryFee.HasValue == true)
                            reg.AmountPaid = reg.Event.EntryFee.Value;
                        await _context.SaveChangesAsync();
                        await SendEventPaymentEmailsAsync(reg);
                    }
                }
            }
        }

        private async Task SendMembershipPaymentEmailsAsync(MembershipApplication application)
        {
            var primaryMember = application.Members.FirstOrDefault(m => m.IsPrimary);
            var applicantName = primaryMember != null ? $"{primaryMember.FirstName} {primaryMember.Surname}" : "Applicant";

            var accountsBody = $@"
                <h2>Payment Received - Application #{application.Id}</h2>
                <p><strong>Applicant:</strong> {applicantName}</p>
                <p><strong>Membership Type:</strong> {application.MembershipType}</p>
                <p><strong>Amount Paid:</strong> R{application.TotalAmount:F2}</p>
                <p><strong>Status:</strong> {application.Status}</p>
                <p>Payment has been confirmed via Yoco.</p>";

            await _emailService.SendEmailAsync(
                AccountsEmail,
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

        private async Task SendEventPaymentEmailsAsync(EventRegistration reg)
        {
            if (reg.Event != null)
                await EventEmailTemplates.SendEventPaymentConfirmedAsync(_emailService, reg, reg.Event);
        }
    }
}

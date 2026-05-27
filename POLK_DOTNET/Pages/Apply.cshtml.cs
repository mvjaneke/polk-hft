using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace POLK_DOTNET.Pages
{
    [ValidateAntiForgeryToken]
    public class ApplyModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly YocoCheckoutService _yocoService;
        private readonly IkhokhaPaymentService _ikhokhaService;
        private readonly EmailService _emailService;
        private readonly ApplicationPdfService _pdfService;

        public ApplyModel(ApplicationDbContext context, YocoCheckoutService yocoService, IkhokhaPaymentService ikhokhaService, EmailService emailService, ApplicationPdfService pdfService)
        {
            _context = context;
            _yocoService = yocoService;
            _ikhokhaService = ikhokhaService;
            _emailService = emailService;
            _pdfService = pdfService;
        }

        [BindProperty]
        public MembershipApplication MembershipApplication { get; set; } = null!;

        [BindProperty]
        public List<MemberInput> MemberInputs { get; set; } = new List<MemberInput>();

        [BindProperty]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the declaration.")]
        public bool DeclarationAgreed { get; set; }

        // Properties for displaying membership costs
        public decimal IndividualMembershipCost { get; set; } = 450.00m;
        public decimal FamilyMembershipCost { get; set; } = 750.00m;
        public decimal PensionerMembershipCost { get; set; } = 300.00m;
        public decimal SahftaAdultCost { get; set; } = 200.00m;
        public decimal SahftaChildCost { get; set; } = 140.00m;

        public void OnGet()
        {
            // Initialize with one member for individual or primary family member
            MemberInputs.Add(new MemberInput { IsPrimary = true });
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            // Manual validation for MemberInputs as it's not directly bound to MembershipApplication
            // The validation attributes on MemberInput will be checked by ModelState.IsValid
            if (!ModelState.IsValid || !DeclarationAgreed)
            {
                // If validation fails, return the page with current model to display errors
                // MemberInputs are already bound, so they will be re-rendered.
                return Page();
            }

            // Calculate total amount based on selected membership type and SAHFTA affiliations
            MembershipApplication.TotalAmount = await CalculateTotalAmount();
            MembershipApplication.SubmittedDate = DateTime.UtcNow;

            _context.MembershipApplications.Add(MembershipApplication);
            await _context.SaveChangesAsync();

            foreach (var memberInput in MemberInputs)
            {
                decimal sahftaFee = 0;
                if (memberInput.HasSahftaAffiliation)
                {
                    sahftaFee = memberInput.IsJunior ? SahftaChildCost : SahftaAdultCost;
                }

                var member = new Member
                {
                    MembershipApplicationId = MembershipApplication.Id,
                    FirstName = memberInput.FirstName,
                    Surname = memberInput.Surname,
                    IdNumber = memberInput.IdNumber,
                    Gender = memberInput.Gender,
                    ContactNumber = memberInput.ContactNumber,
                    EmailAddress = memberInput.EmailAddress,
                    IsPrimary = memberInput.IsPrimary,
                    HasSahftaAffiliation = memberInput.HasSahftaAffiliation,
                    IsJunior = memberInput.IsJunior,
                    SahftaFee = sahftaFee
                };
                _context.Members.Add(member);
            }
            await _context.SaveChangesAsync();

            // Reload application with members for email/PDF
            var savedApplication = await _context.MembershipApplications
                .Include(a => a.Members)
                .FirstAsync(a => a.Id == MembershipApplication.Id);

            await SendApplicationEmailsAsync(savedApplication);

            // Attempt online checkout if configured. Gateway picked from SiteSettings.
            if (MembershipApplication.TotalAmount > 0)
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var appId = MembershipApplication.Id;
                var successUrl = $"{baseUrl}/payment-result?status=success&applicationId={appId}";
                var cancelUrl = $"{baseUrl}/payment-result?status=cancelled&applicationId={appId}";
                var failureUrl = $"{baseUrl}/payment-result?status=failed&applicationId={appId}";

                var gateway = (await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == "PaymentGateway"))?.Value ?? "Ikhokha";

                if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
                {
                    var paylink = await _ikhokhaService.CreatePaymentLinkAsync(
                        MembershipApplication.TotalAmount,
                        $"Membership Application #{appId}",
                        externalTransactionId: $"APP-{appId}",
                        requesterUrl: baseUrl,
                        callbackUrl: $"{baseUrl}/api/ikhokha/callback",
                        successPageUrl: successUrl,
                        failurePageUrl: failureUrl,
                        cancelUrl: cancelUrl);

                    if (paylink != null && !string.IsNullOrEmpty(paylink.PaylinkUrl))
                        return Redirect(paylink.PaylinkUrl);
                }
                else
                {
                    var metadata = new Dictionary<string, string>
                    {
                        { "applicationId", appId.ToString() }
                    };

                    var checkout = await _yocoService.CreateCheckoutAsync(
                        MembershipApplication.TotalAmount,
                        $"Membership Application #{appId}",
                        successUrl,
                        cancelUrl,
                        failureUrl,
                        metadata
                    );

                    if (checkout != null && !string.IsNullOrEmpty(checkout.RedirectUrl))
                        return Redirect(checkout.RedirectUrl);
                }
            }

            // Fallback: redirect to home if no gateway configured or amount is 0
            return RedirectToPage("/Index");
        }

        private async Task SendApplicationEmailsAsync(MembershipApplication application)
        {
            var address = $"{application.StreetAddress}";
            if (!string.IsNullOrEmpty(application.ComplexOrBuilding))
                address += $", {application.ComplexOrBuilding}";
            address += $", {application.Suburb}, {application.City}, {application.Province}, {application.PostalCode}";

            var membersHtml = string.Join("", application.Members.Select(m =>
                $"<tr><td style='padding:8px;border:1px solid #ddd;'>{m.FirstName} {m.Surname}{(m.IsPrimary ? " (Primary)" : "")}</td>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{m.IdNumber}</td>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{m.EmailAddress}</td>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{m.ContactNumber}</td>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{(m.HasSahftaAffiliation ? $"Yes (R{m.SahftaFee:F2})" : "No")}</td></tr>"));

            var emailBody = $@"
                <h2>New Membership Application #{application.Id}</h2>
                <p><strong>Date:</strong> {application.SubmittedDate:dd MMMM yyyy}</p>
                <p><strong>Type:</strong> {application.MembershipType}</p>
                <p><strong>Total Amount:</strong> R{application.TotalAmount:F2}</p>
                <p><strong>Payment Status:</strong> Awaiting Payment</p>
                <h3>Address</h3>
                <p>{address}</p>
                <h3>Members</h3>
                <table style='border-collapse:collapse;width:100%;'>
                    <tr style='background:#f2f2f2;'>
                        <th style='padding:8px;border:1px solid #ddd;text-align:left;'>Name</th>
                        <th style='padding:8px;border:1px solid #ddd;text-align:left;'>ID Number</th>
                        <th style='padding:8px;border:1px solid #ddd;text-align:left;'>Email</th>
                        <th style='padding:8px;border:1px solid #ddd;text-align:left;'>Contact</th>
                        <th style='padding:8px;border:1px solid #ddd;text-align:left;'>SAHFTA</th>
                    </tr>
                    {membersHtml}
                </table>";

            // Send notification to accounts
            await _emailService.SendEmailAsync(
                "accounts@polk-hft.co.za",
                $"New Membership Application #{application.Id} - {application.MembershipType}",
                emailBody);

            // Send PDF copy to applicant
            var primaryMember = application.Members.FirstOrDefault(m => m.IsPrimary);
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
                    <h2>Your Membership Application</h2>
                    <p>Dear {primaryMember.FirstName},</p>
                    <p>Thank you for submitting your membership application to POLK.</p>
                    <p><strong>Application #:</strong> {application.Id}</p>
                    <p><strong>Membership Type:</strong> {application.MembershipType}</p>
                    <p><strong>Total Amount:</strong> R{application.TotalAmount:F2}</p>
                    <p><strong>Payment Status:</strong> Awaiting Payment</p>
                    <p>Please find a copy of your application attached as a PDF.</p>
                    <p>Once payment is completed, you will receive a confirmation email.</p>
                    <br/>
                    <p>Kind regards,<br/>Pretoria Oos Lug Geweer Klub</p>";

                await _emailService.SendEmailAsync(
                    primaryMember.EmailAddress,
                    $"Your Membership Application #{application.Id} - POLK",
                    applicantBody,
                    attachments);
            }
        }

        private async Task<decimal> CalculateTotalAmount()
        {
            decimal total = 0;

            if (MembershipApplication.MembershipType == "Individual")
            {
                total += IndividualMembershipCost;
            }
            else if (MembershipApplication.MembershipType == "Family")
            {
                total += FamilyMembershipCost;
            }
            else if (MembershipApplication.MembershipType == "Pensioner")
            {
                total += PensionerMembershipCost;
            }

            foreach (var memberInput in MemberInputs)
            {
                if (memberInput.HasSahftaAffiliation)
                {
                    total += memberInput.IsJunior ? SahftaChildCost : SahftaAdultCost;
                }
            }

            // Admin Fee Logic
            var primaryApplicantIdNumber = MemberInputs.FirstOrDefault(m => m.IsPrimary)?.IdNumber;
            if (!string.IsNullOrEmpty(primaryApplicantIdNumber))
            {
                var memberExists = await _context.Members
                    .AnyAsync(m => m.IdNumber == primaryApplicantIdNumber && m.MembershipApplication.Status == "Approved");

                if (!memberExists)
                {
                    var adminFeeOption = await _context.MembershipOptions
                        .FirstOrDefaultAsync(mo => mo.Title.Contains("One Time Admin fee"));

                    if (adminFeeOption != null)
                    {
                        string priceString = adminFeeOption.Price.Replace("R", "").Replace("/month", "").Trim();
                        if (decimal.TryParse(priceString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal adminFee))
                        {
                            total += adminFee;
                        }
                    }
                }
            }

            return total;
        }

        // Helper class for binding member data from the form
        public class MemberInput
        {
            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string FirstName { get; set; } = null!;

            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string Surname { get; set; } = null!;

            [Required]
            [StringLength(20, MinimumLength = 5)]
            public string IdNumber { get; set; } = null!;

            [Required]
            public string Gender { get; set; } = null!;

            [Required]
            [Phone]
            public string ContactNumber { get; set; } = null!;

            [Required]
            [EmailAddress]
            public string EmailAddress { get; set; } = null!;

            public bool IsPrimary { get; set; }

            public bool HasSahftaAffiliation { get; set; }

            public bool IsJunior { get; set; }
        }
    }
}

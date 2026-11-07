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
        private readonly MembershipService _membership;
        private readonly IkhokhaPaymentService _ikhokha;
        private readonly ILogger<PaymentResultModel> _logger;

        public PaymentResultModel(ApplicationDbContext context, EmailService emailService, MembershipService membership, IkhokhaPaymentService ikhokha, ILogger<PaymentResultModel> logger)
        {
            _context = context;
            _emailService = emailService;
            _membership = membership;
            _ikhokha = ikhokha;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Status { get; set; } = "failed";

        [BindProperty(SupportsGet = true)]
        public int? ApplicationId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        /// <summary>Set on a retried card payment; picks out that attempt's iKhokha reference.</summary>
        [BindProperty(SupportsGet = true)]
        public string? Attempt { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? RegistrationId { get; set; }

        public string? EventTitle { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (ApplicationId.HasValue)
                return await HandleMembershipAsync(ApplicationId.Value);

            if (Status != "success") return Page();

            if (RegistrationId.HasValue)
                await HandleEventAsync(RegistrationId.Value);

            return Page();
        }

        /// <summary>
        /// Card payment return for an event booking. As with membership, the return URL is not
        /// proof of payment (anyone can type it): with iKhokha the booking is only marked paid
        /// once the gateway confirms it. If it hasn't yet, the page says so and the signed
        /// webhook marks it paid when the confirmation arrives.
        /// </summary>
        private async Task HandleEventAsync(int registrationId)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.Event)
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.Id == registrationId);
            if (reg == null) return;

            EventTitle = reg.Event?.Title;
            if (reg.Status == "Paid") return;

            var gateway = (await _membership.GetSettingAsync("PaymentGateway")) ?? "Ikhokha";
            if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
            {
                IkhokhaStatusResponse? status = null;
                try { status = await _ikhokha.GetStatusByExternalTxnAsync($"EVT-{registrationId}"); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not check iKhokha status for event registration {Id}", registrationId); }

                if (status?.IsPaid != true)
                {
                    _logger.LogInformation("Event registration {Id}: return page reached but iKhokha has not confirmed payment (status {Status}); leaving it to the webhook",
                        registrationId, status?.Status ?? "unknown");
                    Status = "pending";
                    return;
                }
            }
            // Yoco has no status check wired up here and no webhook for event bookings; keep the previous behaviour.

            reg.Status = "Paid";
            if (reg.Event != null)
                reg.AmountPaid = EventFeeCalculator.ForBooking(reg.Event, reg);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Event registration {Id} marked Paid from the payment return page ({Gateway})", registrationId, gateway);
            await SendEventPaymentEmailsAsync(reg);
        }

        /// <summary>
        /// Card payment return for a membership application. The return URL alone is not proof
        /// of payment (anyone can type it), so with iKhokha the payment is checked with the
        /// gateway before the membership is recorded; if it isn't confirmed yet, the webhook
        /// will do it. The applicant then lands on their own application page.
        /// </summary>
        private async Task<IActionResult> HandleMembershipAsync(int applicationId)
        {
            var app = await _context.MembershipApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId);
            var tokenOk = app != null && !string.IsNullOrEmpty(Token) && app.AccessToken == Token;

            var outcome = Status switch { "success" => "success", "cancelled" => "cancelled", _ => "failed" };

            if (app != null && Status == "success" && !app.IsPaid)
            {
                var gateway = (await _membership.GetSettingAsync("PaymentGateway")) ?? "Ikhokha";
                if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
                {
                    IkhokhaStatusResponse? status = null;
                    try
                    {
                        var attempt = Attempt != null && Attempt.Length <= 20 && Attempt.All(char.IsAsciiDigit) ? Attempt : null;
                        status = await _ikhokha.GetStatusByExternalTxnAsync(MembershipCardPaymentService.ExternalReference(applicationId, attempt));
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Could not check iKhokha status for application {Id}", applicationId); }

                    if (status?.IsPaid == true)
                        await _membership.MarkPaidAsync(applicationId, "Online");
                    else
                        outcome = "pending";
                }
                else
                {
                    // Yoco has no status check wired up here; keep the previous behaviour.
                    await _membership.MarkPaidAsync(applicationId, "Online");
                }
            }

            if (tokenOk)
                return RedirectToPage("/ApplicationSubmitted", new { token = app!.AccessToken, payment = outcome });

            if (outcome == "pending") Status = "success";
            return Page();
        }

        private async Task SendEventPaymentEmailsAsync(EventRegistration reg)
        {
            if (reg.Event != null)
                await EventEmailTemplates.SendEventPaymentConfirmedAsync(_emailService, reg, reg.Event);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace POLK_DOTNET.Pages
{
    public class RegisterEventModel : PageModel
    {
        private const string AccountsEmail = "accounts@polk-hft.co.za";

        private readonly ApplicationDbContext _context;
        private readonly YocoCheckoutService _yocoService;
        private readonly EmailService _emailService;

        public RegisterEventModel(ApplicationDbContext context, YocoCheckoutService yocoService, EmailService emailService)
        {
            _context = context;
            _yocoService = yocoService;
            _emailService = emailService;
        }

        [BindProperty]
        public EventRegistration EventRegistration { get; set; } = new EventRegistration();

        [BindProperty(SupportsGet = true)]
        public int EventId { get; set; }

        public Event Event { get; set; } = null!;

        public bool RegistrationClosed { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (EventId == 0) return RedirectToPage("/Index");

            var ev = await _context.Events.FindAsync(EventId);
            if (ev == null) return NotFound();

            Event = ev;
            RegistrationClosed = !IsOpenForRegistration(ev);
            EventRegistration.EventId = EventId;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var ev = await _context.Events.FindAsync(EventId);
            if (ev == null) return NotFound();

            Event = ev;
            RegistrationClosed = !IsOpenForRegistration(ev);

            if (RegistrationClosed)
            {
                ModelState.AddModelError(string.Empty, "Registration for this event is closed.");
                return Page();
            }

            EventRegistration.RegistrationDate = DateTime.UtcNow;
            EventRegistration.EventId = EventId;

            ModelState.Remove("EventRegistration.Event");
            ModelState.Remove("EventRegistration.Participants");

            if (!ev.RequiresAttendanceType) ModelState.Remove("EventRegistration.AttendanceType");
            if (!ev.RequiresSahfta) ModelState.Remove("EventRegistration.SAHFTANumber");
            if (!ev.RequiresClubName) ModelState.Remove("EventRegistration.ClubName");
            if (!ev.RequiresDivision) ModelState.Remove("EventRegistration.Division");
            if (!ev.AllowsClubRifle) ModelState.Remove("EventRegistration.RifleOwnership");

            if (ev.RequiresAttendanceType && string.IsNullOrWhiteSpace(EventRegistration.AttendanceType))
                ModelState.AddModelError("EventRegistration.AttendanceType", "Please select Competitor or Spectator.");

            if (ev.RequiresDivision && string.IsNullOrWhiteSpace(EventRegistration.Division))
                ModelState.AddModelError("EventRegistration.Division", "Division is required.");

            if (ev.AllowsClubRifle && string.IsNullOrWhiteSpace(EventRegistration.RifleOwnership))
                ModelState.AddModelError("EventRegistration.RifleOwnership", "Please select own or club rifle.");

            if (!EventRegistration.InfoAccurateConfirmed)
                ModelState.AddModelError("EventRegistration.InfoAccurateConfirmed", "You must confirm your information is accurate.");

            if (!EventRegistration.IndemnityAgreed)
                ModelState.AddModelError("EventRegistration.IndemnityAgreed", "You must agree to the indemnity.");

            if (string.IsNullOrWhiteSpace(EventRegistration.SocialMediaConsent))
                ModelState.AddModelError("EventRegistration.SocialMediaConsent", "Please select a social media consent option.");

            // Payment method validation
            var hasFee = ev.EntryFee.HasValue && ev.EntryFee.Value > 0;
            if (hasFee)
            {
                if (string.IsNullOrWhiteSpace(EventRegistration.PaymentMethod))
                    ModelState.AddModelError("EventRegistration.PaymentMethod", "Please select a payment method.");
                else if (EventRegistration.PaymentMethod == "Yoco" && !ev.EnableYocoPayment)
                    ModelState.AddModelError("EventRegistration.PaymentMethod", "Online payment is not available for this event.");
            }
            else
            {
                ModelState.Remove("EventRegistration.PaymentMethod");
            }

            if (!ModelState.IsValid) return Page();

            // Set status + amount based on fee presence
            if (hasFee)
            {
                EventRegistration.AmountPaid = 0; // will be set on payment confirmation
                EventRegistration.Status = "Pending";
            }
            else
            {
                EventRegistration.PaymentMethod = "None";
                EventRegistration.Status = "Confirmed";
            }

            _context.EventRegistrations.Add(EventRegistration);
            await _context.SaveChangesAsync();

            // Send initial emails (fire-and-forget errors shouldn't block registration)
            await SendInitialEmailsAsync(EventRegistration, ev);

            // If Yoco payment: create checkout and redirect
            if (hasFee && EventRegistration.PaymentMethod == "Yoco" && ev.EnableYocoPayment)
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var regId = EventRegistration.Id;
                var metadata = new Dictionary<string, string>
                {
                    { "eventRegistrationId", regId.ToString() },
                    { "eventId", ev.Id.ToString() }
                };

                var checkout = await _yocoService.CreateCheckoutAsync(
                    ev.EntryFee!.Value,
                    $"{ev.Title} - Registration #{regId}",
                    $"{baseUrl}/payment-result?status=success&registrationId={regId}",
                    $"{baseUrl}/payment-result?status=cancelled&registrationId={regId}",
                    $"{baseUrl}/payment-result?status=failed&registrationId={regId}",
                    metadata
                );

                if (checkout != null && !string.IsNullOrEmpty(checkout.RedirectUrl))
                {
                    EventRegistration.YocoCheckoutId = checkout.Id;
                    await _context.SaveChangesAsync();
                    return Redirect(checkout.RedirectUrl);
                }
                // Yoco failed — fall through to confirmation; admin will reconcile manually
            }

            return RedirectToPage("/RegistrationConfirmation", new { id = EventRegistration.Id });
        }

        private static bool IsOpenForRegistration(Event ev)
        {
            if (!ev.IsRegistrationOpen) return false;
            if (ev.RegistrationCloseDate.HasValue && DateTime.UtcNow.Date > ev.RegistrationCloseDate.Value.Date)
                return false;
            return true;
        }

        private async Task SendInitialEmailsAsync(EventRegistration reg, Event ev)
        {
            var feeText = ev.EntryFee.HasValue && ev.EntryFee.Value > 0
                ? $"R{ev.EntryFee.Value:F2}" + (!string.IsNullOrWhiteSpace(ev.EntryFeeDescription) ? $" ({ev.EntryFeeDescription})" : "")
                : "No fee";

            var paymentInstructions = BuildPaymentInstructions(reg, ev);

            var registrantBody = $@"
                <h2>Registration Received</h2>
                <p>Dear {reg.Name},</p>
                <p>Thank you for registering for <strong>{ev.Title}</strong>.</p>
                <p><strong>Event date:</strong> {ev.StartDate:dd MMMM yyyy}</p>
                <p><strong>Time:</strong> {ev.Time}</p>
                <p><strong>Location:</strong> {ev.Location}</p>
                <p><strong>Entry fee:</strong> {feeText}</p>
                <p><strong>Status:</strong> {reg.Status}</p>
                {paymentInstructions}
                <br/>
                <p>Kind regards,<br/>Pretoria Oos Lug Geweer Klub</p>";

            await _emailService.SendEmailAsync(
                reg.EmailAddress,
                $"Registration Received - {ev.Title}",
                registrantBody);

            var accountsBody = $@"
                <h2>New Event Registration #{reg.Id}</h2>
                <p><strong>Event:</strong> {ev.Title} ({ev.StartDate:dd MMM yyyy})</p>
                <p><strong>Registrant:</strong> {reg.Name} {reg.Surname}</p>
                <p><strong>Email:</strong> {reg.EmailAddress}</p>
                <p><strong>Cell:</strong> {reg.CellNumber}</p>
                <p><strong>ID:</strong> {reg.IdNumber}</p>
                <p><strong>Gun Type:</strong> {reg.GunType}</p>
                {(string.IsNullOrWhiteSpace(reg.AttendanceType) ? "" : $"<p><strong>Attendance:</strong> {reg.AttendanceType}</p>")}
                {(string.IsNullOrWhiteSpace(reg.Division) ? "" : $"<p><strong>Division:</strong> {reg.Division}{(string.IsNullOrWhiteSpace(reg.OtherDivision) ? "" : $" ({reg.OtherDivision})")}</p>")}
                {(string.IsNullOrWhiteSpace(reg.SAHFTANumber) ? "" : $"<p><strong>SAHFTA:</strong> {reg.SAHFTANumber}</p>")}
                {(string.IsNullOrWhiteSpace(reg.ClubName) ? "" : $"<p><strong>Club:</strong> {reg.ClubName}</p>")}
                {(string.IsNullOrWhiteSpace(reg.RifleOwnership) ? "" : $"<p><strong>Rifle:</strong> {reg.RifleOwnership}</p>")}
                <p><strong>Payment method:</strong> {reg.PaymentMethod ?? "N/A"}</p>
                <p><strong>Status:</strong> {reg.Status}</p>";

            var notifyEmail = string.IsNullOrWhiteSpace(ev.NotificationEmail) ? AccountsEmail : ev.NotificationEmail;
            await _emailService.SendEmailAsync(
                notifyEmail,
                $"New Registration - {ev.Title} - #{reg.Id}",
                accountsBody);
        }

        private static string BuildPaymentInstructions(EventRegistration reg, Event ev)
        {
            if (!ev.EntryFee.HasValue || ev.EntryFee.Value <= 0)
                return "<p>No payment required for this event.</p>";

            return reg.PaymentMethod switch
            {
                "Yoco" => "<p>You will be redirected to our online payment provider. Your registration will be confirmed once payment is received.</p>",
                "EFT" => !string.IsNullOrWhiteSpace(ev.BankingDetailsHtml)
                    ? $"<p><strong>Please make payment via EFT:</strong></p><div>{ev.BankingDetailsHtml}</div><p>Please reply to this email with your proof of payment.</p>"
                    : "<p>Please contact the club for banking details and reply with proof of payment.</p>",
                "AtVenue" => "<p>Please pay on arrival at the venue (card machine or cash available).</p>",
                _ => ""
            };
        }
    }
}

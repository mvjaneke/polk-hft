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
        private readonly IkhokhaPaymentService _ikhokhaService;
        private readonly EmailService _emailService;
        private readonly SahftaMembersClient _sahftaClient;

        public RegisterEventModel(ApplicationDbContext context, YocoCheckoutService yocoService, IkhokhaPaymentService ikhokhaService, EmailService emailService, SahftaMembersClient sahftaClient)
        {
            _context = context;
            _yocoService = yocoService;
            _ikhokhaService = ikhokhaService;
            _emailService = emailService;
            _sahftaClient = sahftaClient;
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
            if (!ev.IsClubEvent) return NotFound();

            Event = ev;
            RegistrationClosed = !IsOpenForRegistration(ev);
            EventRegistration.EventId = EventId;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var ev = await _context.Events.FindAsync(EventId);
            if (ev == null) return NotFound();
            if (!ev.IsClubEvent) return NotFound();

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
            if (!ev.IsDoubleHeader) ModelState.Remove("EventRegistration.ShootSelection");

            if (ev.RequiresAttendanceType && string.IsNullOrWhiteSpace(EventRegistration.AttendanceType))
                ModelState.AddModelError("EventRegistration.AttendanceType", "Please select Competitor or Spectator.");

            // Spectators attend but don't shoot — clear competition-only fields and skip their validation.
            // They still complete contact details and sign the indemnity & media consent below.
            bool isSpectator = ev.RequiresAttendanceType &&
                string.Equals(EventRegistration.AttendanceType, "Spectator", StringComparison.OrdinalIgnoreCase);
            if (isSpectator)
            {
                EventRegistration.GunType = "N/A";
                EventRegistration.RifleOwnership = null;
                EventRegistration.Division = null;
                EventRegistration.OtherDivision = null;
                EventRegistration.ShootSelection = null;
                ModelState.Remove("EventRegistration.GunType");
            }

            if (ev.RequiresDivision && !isSpectator && string.IsNullOrWhiteSpace(EventRegistration.Division))
                ModelState.AddModelError("EventRegistration.Division", "Division is required.");

            if (ev.AllowsClubRifle && !isSpectator && string.IsNullOrWhiteSpace(EventRegistration.RifleOwnership))
                ModelState.AddModelError("EventRegistration.RifleOwnership", "Please select own or club rifle.");

            if (ev.IsDoubleHeader && !isSpectator)
            {
                var sel = EventRegistration.ShootSelection;
                if (sel != "First" && sel != "Second" && sel != "Both")
                    ModelState.AddModelError("EventRegistration.ShootSelection", "Please choose which shoot(s) you will enter.");
            }
            else
            {
                EventRegistration.ShootSelection = null;
            }

            if (!EventRegistration.InfoAccurateConfirmed)
                ModelState.AddModelError("EventRegistration.InfoAccurateConfirmed", "You must confirm your information is accurate.");

            if (!EventRegistration.IndemnityAgreed)
                ModelState.AddModelError("EventRegistration.IndemnityAgreed", "You must agree to the indemnity.");

            if (string.IsNullOrWhiteSpace(EventRegistration.SocialMediaConsent))
                ModelState.AddModelError("EventRegistration.SocialMediaConsent", "Please select a social media consent option.");

            // Compute the effective fee server-side (single, both, or single-shoot of a double header).
            // Spectators never pay an entry fee.
            var effectiveFee = isSpectator
                ? 0m
                : ComputeEffectiveFee(ev, EventRegistration.ShootSelection, EventRegistration.RifleOwnership);
            var hasFee = effectiveFee > 0;

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

            if (hasFee)
            {
                EventRegistration.AmountPaid = 0; // set on payment confirmation
                EventRegistration.Status = "Pending";
            }
            else
            {
                EventRegistration.PaymentMethod = "None";
                EventRegistration.Status = "Confirmed";
            }

            await EnrichFromSahftaAsync(EventRegistration);

            _context.EventRegistrations.Add(EventRegistration);
            await _context.SaveChangesAsync();

            await SendInitialEmailsAsync(EventRegistration, ev, effectiveFee);

            if (hasFee && EventRegistration.PaymentMethod == "Yoco" && ev.EnableYocoPayment)
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var regId = EventRegistration.Id;
                var successUrl = $"{baseUrl}/payment-result?status=success&registrationId={regId}";
                var cancelUrl = $"{baseUrl}/payment-result?status=cancelled&registrationId={regId}";
                var failureUrl = $"{baseUrl}/payment-result?status=failed&registrationId={regId}";

                var gateway = (await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == "PaymentGateway"))?.Value ?? "Ikhokha";

                if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
                {
                    var paylink = await _ikhokhaService.CreatePaymentLinkAsync(
                        effectiveFee,
                        $"{ev.Title} - Registration #{regId}",
                        externalTransactionId: $"EVT-{regId}",
                        requesterUrl: baseUrl,
                        callbackUrl: $"{baseUrl}/api/ikhokha/callback",
                        successPageUrl: successUrl,
                        failurePageUrl: failureUrl,
                        cancelUrl: cancelUrl);

                    if (paylink != null && !string.IsNullOrEmpty(paylink.PaylinkUrl))
                    {
                        EventRegistration.YocoCheckoutId = paylink.PaylinkID;
                        await _context.SaveChangesAsync();
                        return Redirect(paylink.PaylinkUrl);
                    }
                }
                else
                {
                    var metadata = new Dictionary<string, string>
                    {
                        { "eventRegistrationId", regId.ToString() },
                        { "eventId", ev.Id.ToString() }
                    };

                    var checkout = await _yocoService.CreateCheckoutAsync(
                        effectiveFee,
                        $"{ev.Title} - Registration #{regId}",
                        successUrl,
                        cancelUrl,
                        failureUrl,
                        metadata
                    );

                    if (checkout != null && !string.IsNullOrEmpty(checkout.RedirectUrl))
                    {
                        EventRegistration.YocoCheckoutId = checkout.Id;
                        await _context.SaveChangesAsync();
                        return Redirect(checkout.RedirectUrl);
                    }
                }
            }

            return RedirectToPage("/RegistrationConfirmation", new { id = EventRegistration.Id });
        }

        private async Task EnrichFromSahftaAsync(EventRegistration reg)
        {
            // 1. Try name + surname first (only returns when there's an exact or unambiguous match)
            var api = await _sahftaClient.LookupByNameAsync(reg.Name, reg.Surname);

            // 2. Fall back to SAHFTA membership number if supplied
            if (api == null &&
                !string.IsNullOrWhiteSpace(reg.SAHFTANumber) &&
                !reg.SAHFTANumber.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !reg.SAHFTANumber.Equals("n/a", StringComparison.OrdinalIgnoreCase) &&
                reg.SAHFTANumber != "0")
            {
                api = await _sahftaClient.LookupByMembershipNumberAsync(reg.SAHFTANumber);
            }

            if (api == null) return;

            // Overwrite club and membership # with the canonical values from the API.
            if (!string.IsNullOrWhiteSpace(api.club))
                reg.ClubName = api.club;
            if (!string.IsNullOrWhiteSpace(api.membershipNumber))
                reg.SAHFTANumber = api.membershipNumber;
        }

        public static decimal ComputeEffectiveFee(Event ev, string? shootSelection, string? rifleOwnership = null)
        {
            var single = ev.EntryFee ?? 0;
            decimal shootFee;
            if (!ev.IsDoubleHeader)
                shootFee = single;
            else if (string.Equals(shootSelection, "Both", StringComparison.OrdinalIgnoreCase))
                shootFee = ev.DoubleHeaderFee ?? (single * 2);
            else
                shootFee = single;

            var rifleFee = (ev.AllowsClubRifle && string.Equals(rifleOwnership, "Club", StringComparison.OrdinalIgnoreCase))
                ? (ev.ClubRifleFee ?? 0m)
                : 0m;

            return shootFee + rifleFee;
        }

        private static bool IsOpenForRegistration(Event ev)
        {
            if (!ev.IsRegistrationOpen) return false;
            if (ev.RegistrationCloseDate.HasValue && DateTime.UtcNow.Date > ev.RegistrationCloseDate.Value.Date)
                return false;
            return true;
        }

        private async Task SendInitialEmailsAsync(EventRegistration reg, Event ev, decimal effectiveFee)
        {
            var usesClubRifle = ev.AllowsClubRifle && string.Equals(reg.RifleOwnership, "Club", StringComparison.OrdinalIgnoreCase) && ev.ClubRifleFee.HasValue && ev.ClubRifleFee.Value > 0;
            var feeText = effectiveFee > 0
                ? $"R{effectiveFee:F2}" + (!string.IsNullOrWhiteSpace(ev.EntryFeeDescription) ? $" ({ev.EntryFeeDescription})" : "")
                  + (usesClubRifle ? $" — includes R{ev.ClubRifleFee!.Value:F2} club rifle &amp; pellets" : "")
                : "No fee";

            var shootLine = ev.IsDoubleHeader && !string.IsNullOrWhiteSpace(reg.ShootSelection)
                ? $"<p><strong>Shoot(s):</strong> {FormatShoot(reg.ShootSelection)}</p>"
                : "";

            var paymentInstructions = BuildPaymentInstructions(reg, ev);

            var registrantBody = $@"
                <h2>Registration Received</h2>
                <p>Dear {reg.Name},</p>
                <p>Thank you for registering for <strong>{ev.Title}</strong>.</p>
                <p><strong>Event date:</strong> {ev.StartDate:dd MMMM yyyy}</p>
                <p><strong>Time:</strong> {ev.Time}</p>
                <p><strong>Location:</strong> {ev.Location}</p>
                {shootLine}
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
                {shootLine}
                <p><strong>Fee:</strong> {feeText}</p>
                <p><strong>Payment method:</strong> {reg.PaymentMethod ?? "N/A"}</p>
                <p><strong>Status:</strong> {reg.Status}</p>";

            var notifyEmail = string.IsNullOrWhiteSpace(ev.NotificationEmail) ? AccountsEmail : ev.NotificationEmail;
            await _emailService.SendEmailAsync(
                notifyEmail,
                $"New Registration - {ev.Title} - #{reg.Id}",
                accountsBody);
        }

        private static string FormatShoot(string? sel) => sel switch
        {
            "First" => "Shoot 1 only",
            "Second" => "Shoot 2 only",
            "Both" => "Both shoots",
            _ => sel ?? ""
        };

        private static string BuildPaymentInstructions(EventRegistration reg, Event ev)
        {
            if (ev.EntryFee is null or <= 0) return "<p>No payment required for this event.</p>";

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

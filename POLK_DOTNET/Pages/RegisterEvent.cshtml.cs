using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace POLK_DOTNET.Pages
{
    public class RegisterEventModel : PageModel
    {
        private const string AccountsEmail = "accounts@polk-hft.co.za";

        // Guard rails on a single booking — big enough for a club or a family, small
        // enough that a bot can't post a thousand participants in one go.
        public const int MaxParticipants = 12;
        public const int MaxExtraMeals = 50;

        private readonly ApplicationDbContext _context;
        private readonly YocoCheckoutService _yocoService;
        private readonly IkhokhaPaymentService _ikhokhaService;
        private readonly EmailService _emailService;
        private readonly SahftaMembersClient _sahftaClient;
        private readonly ILogger<RegisterEventModel> _logger;

        public RegisterEventModel(ApplicationDbContext context, YocoCheckoutService yocoService, IkhokhaPaymentService ikhokhaService, EmailService emailService, SahftaMembersClient sahftaClient, ILogger<RegisterEventModel> logger)
        {
            _context = context;
            _yocoService = yocoService;
            _ikhokhaService = ikhokhaService;
            _emailService = emailService;
            _sahftaClient = sahftaClient;
            _logger = logger;
        }

        [BindProperty]
        public BookingInput Booking { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int EventId { get; set; }

        public Event Event { get; set; } = null!;

        public bool RegistrationClosed { get; private set; }

        // The contact person on a booking. Everyone attending — the contact included —
        // is captured as a ParticipantInput below.
        public class BookingInput
        {
            [Required(ErrorMessage = "First name is required.")]
            [StringLength(100)]
            public string Name { get; set; } = string.Empty;

            [Required(ErrorMessage = "Surname is required.")]
            [StringLength(100)]
            public string Surname { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email address is required.")]
            [EmailAddress(ErrorMessage = "Enter a valid email address.")]
            [StringLength(150)]
            public string EmailAddress { get; set; } = string.Empty;

            [Required(ErrorMessage = "Contact number is required.")]
            [Phone(ErrorMessage = "Enter a valid contact number.")]
            [StringLength(20)]
            public string CellNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "ID number is required.")]
            [StringLength(20)]
            public string IdNumber { get; set; } = string.Empty;

            public int ExtraMeals { get; set; }

            public string? PaymentMethod { get; set; }

            [StringLength(100)]
            public string? PaymentReference { get; set; }

            public List<ParticipantInput> Participants { get; set; } = new();
        }

        // Every field here is nullable on purpose. A non-nullable string picks up an implicit
        // [Required] under <Nullable>enable</Nullable>, which fires during model binding — before
        // ValidateParticipant gets to decide whether the field even applies to this person. A
        // spectator posts no rifle type (the inputs are hidden), so an implicitly-required GunType
        // would wedge the booking on an error the registrant cannot see or clear. Requiredness for
        // a participant is contextual, so it lives in ValidateParticipant and nowhere else.
        public class ParticipantInput
        {
            [StringLength(100)]
            public string? Name { get; set; }

            [StringLength(100)]
            public string? Surname { get; set; }

            [StringLength(20)]
            public string? IdNumber { get; set; }

            public string? AttendanceType { get; set; }
            public string? GunType { get; set; }
            public string? RifleOwnership { get; set; }
            public string? Division { get; set; }

            [StringLength(100)]
            public string? OtherDivision { get; set; }

            [StringLength(50)]
            public string? SAHFTANumber { get; set; }

            [StringLength(100)]
            public string? ClubName { get; set; }

            public string? ShootSelection { get; set; }

            [StringLength(100)]
            public string? GuardianName { get; set; }

            [StringLength(100)]
            public string? GuardianSurname { get; set; }

            public bool InfoAccurateConfirmed { get; set; }
            public bool IndemnityAgreed { get; set; }
            public bool GuardianIndemnityAgreed { get; set; }
            public string? SocialMediaConsent { get; set; }

            public bool IsSpectator(Event ev) =>
                ev.RequiresAttendanceType &&
                string.Equals(AttendanceType, "Spectator", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (EventId == 0) return RedirectToPage("/Index");

            var ev = await _context.Events.FindAsync(EventId);
            if (ev == null) return NotFound();
            if (!ev.IsClubEvent) return NotFound();

            Event = ev;
            RegistrationClosed = !IsOpenForRegistration(ev);
            Booking.Participants.Add(new ParticipantInput());

            return Page();
        }

        // Adding or removing a person leaves ?handler=… in the address bar, because those posts
        // re-render the page instead of redirecting. Reloading from the address bar then arrives
        // here as a GET, which would otherwise be a 400. Send them back to a clean form.
        public IActionResult OnGetAddParticipant() => RedirectToPage(new { eventId = EventId });

        public IActionResult OnGetRemoveParticipant() => RedirectToPage(new { eventId = EventId });

        // "Add another person" / "Remove". Both re-render the form rather than saving, so
        // they deliberately skip validation — the registrant is still filling it in. State
        // is carried by the posted model (no Alpine.js, per the project conventions), and
        // ModelState is cleared so the re-rendered inputs come from the model and not from
        // now-misaligned posted values after a removal.
        public async Task<IActionResult> OnPostAddParticipantAsync()
        {
            var ev = await LoadEventAsync();
            if (ev == null) return NotFound();

            if (Booking.Participants.Count < MaxParticipants)
                Booking.Participants.Add(new ParticipantInput());

            ModelState.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveParticipantAsync(int index)
        {
            var ev = await LoadEventAsync();
            if (ev == null) return NotFound();

            if (Booking.Participants.Count > 1 && index >= 0 && index < Booking.Participants.Count)
                Booking.Participants.RemoveAt(index);

            ModelState.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var ev = await LoadEventAsync();
            if (ev == null) return NotFound();

            if (RegistrationClosed)
            {
                ModelState.AddModelError(string.Empty, "Registration for this event is closed.");
                return Page();
            }

            if (Booking.Participants.Count == 0)
                Booking.Participants.Add(new ParticipantInput());

            if (Booking.Participants.Count > MaxParticipants)
            {
                ModelState.AddModelError(string.Empty, $"A single booking can cover at most {MaxParticipants} people.");
                return Page();
            }

            ValidateMeals(ev);
            for (int i = 0; i < Booking.Participants.Count; i++)
                ValidateParticipant(ev, Booking.Participants[i], i);

            var participants = Booking.Participants
                .Select((p, i) => ToEntity(ev, p, i + 1))
                .ToList();

            var total = EventFeeCalculator.ForBooking(ev, participants, Booking.ExtraMeals);
            var hasFee = total > 0;

            if (hasFee)
            {
                if (string.IsNullOrWhiteSpace(Booking.PaymentMethod))
                    ModelState.AddModelError("Booking.PaymentMethod", "Please select a payment method.");
                else if (Booking.PaymentMethod == "Yoco" && !ev.EnableYocoPayment)
                    ModelState.AddModelError("Booking.PaymentMethod", "Online payment is not available for this event.");
            }

            if (!ModelState.IsValid) return Page();

            var registration = new EventRegistration
            {
                EventId = ev.Id,
                RegistrationDate = DateTime.UtcNow,
                Name = Booking.Name.Trim(),
                Surname = Booking.Surname.Trim(),
                EmailAddress = Booking.EmailAddress.Trim(),
                CellNumber = Booking.CellNumber.Trim(),
                IdNumber = Booking.IdNumber.Trim(),
                ExtraMeals = Booking.ExtraMeals,
                PaymentReference = string.IsNullOrWhiteSpace(Booking.PaymentReference) ? null : Booking.PaymentReference.Trim(),
                Participants = participants
            };

            if (hasFee)
            {
                registration.PaymentMethod = Booking.PaymentMethod;
                registration.AmountPaid = 0; // set on payment confirmation
                registration.Status = "Pending";
            }
            else
            {
                registration.PaymentMethod = "None";
                registration.Status = "Confirmed";
            }

            foreach (var p in participants)
                await EnrichFromSahftaAsync(p);

            _context.EventRegistrations.Add(registration);
            await _context.SaveChangesAsync();

            await SendInitialEmailsAsync(registration, ev, total);

            if (hasFee && registration.PaymentMethod == "Yoco" && ev.EnableYocoPayment)
            {
                var redirect = await StartOnlinePaymentAsync(registration, ev, total);
                if (redirect != null) return redirect;

                // No redirect means the gateway never gave us a payment link — unconfigured
                // credentials, an unreachable callback URL (it rejects localhost), or a
                // rejected request. The booking is saved and still owed, so say so on the
                // confirmation page instead of quietly showing a page that reads as settled.
                _logger.LogError(
                    "Online payment could not be started for registration {RegistrationId} on event {EventId} ({Amount:F2}). Registrant was asked to pay another way.",
                    registration.Id, ev.Id, total);
                TempData["PaymentStartFailed"] = "true";
            }

            return RedirectToPage("/RegistrationConfirmation", new { id = registration.Id });
        }

        private async Task<Event?> LoadEventAsync()
        {
            var ev = await _context.Events.FindAsync(EventId);
            if (ev == null || !ev.IsClubEvent) return null;

            Event = ev;
            RegistrationClosed = !IsOpenForRegistration(ev);
            return ev;
        }

        private void ValidateMeals(Event ev)
        {
            if (!ev.OffersMeals)
            {
                Booking.ExtraMeals = 0;
                return;
            }

            if (Booking.ExtraMeals < 0)
                ModelState.AddModelError("Booking.ExtraMeals", "Number of meals cannot be negative.");
            else if (Booking.ExtraMeals > MaxExtraMeals)
                ModelState.AddModelError("Booking.ExtraMeals", $"Please book at most {MaxExtraMeals} meals, or contact the club directly.");
        }

        // Validates one person and normalises their fields in place. Spectators don't shoot,
        // so their competition-only answers are cleared and skipped — but they still sign
        // their own indemnity and media consent.
        private void ValidateParticipant(Event ev, ParticipantInput p, int index)
        {
            var key = $"Booking.Participants[{index}]";
            var who = string.IsNullOrWhiteSpace(p.Name) && string.IsNullOrWhiteSpace(p.Surname)
                ? $"Person {index + 1}"
                : $"{p.Name} {p.Surname}".Trim();

            if (string.IsNullOrWhiteSpace(p.Name))
                ModelState.AddModelError($"{key}.Name", "First name is required.");
            if (string.IsNullOrWhiteSpace(p.Surname))
                ModelState.AddModelError($"{key}.Surname", "Surname is required.");
            if (string.IsNullOrWhiteSpace(p.IdNumber))
                ModelState.AddModelError($"{key}.IdNumber", "ID number is required.");

            if (ev.RequiresAttendanceType && string.IsNullOrWhiteSpace(p.AttendanceType))
                ModelState.AddModelError($"{key}.AttendanceType", $"Select whether {who} is a competitor or a spectator.");

            if (p.IsSpectator(ev))
            {
                p.GunType = "N/A";
                p.RifleOwnership = null;
                p.Division = null;
                p.OtherDivision = null;
                p.ShootSelection = null;

                // The competition inputs are hidden for a spectator, so anything the binder
                // recorded against them is an error the registrant can neither see nor clear.
                foreach (var field in SpectatorIrrelevantFields)
                    ModelState.Remove($"{key}.{field}");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(p.GunType))
                    ModelState.AddModelError($"{key}.GunType", "Rifle type is required.");

                if (ev.RequiresDivision && string.IsNullOrWhiteSpace(p.Division))
                    ModelState.AddModelError($"{key}.Division", "Division is required.");

                if (ev.AllowsClubRifle && string.IsNullOrWhiteSpace(p.RifleOwnership))
                    ModelState.AddModelError($"{key}.RifleOwnership", "Please select own or club rifle.");

                if (ev.IsDoubleHeader)
                {
                    var sel = p.ShootSelection;
                    if (sel != "First" && sel != "Second" && sel != "Both")
                        ModelState.AddModelError($"{key}.ShootSelection", $"Choose which shoot(s) {who} will enter.");
                }
                else
                {
                    // Provincial two-day events enter every competitor for both days.
                    p.ShootSelection = null;
                }
            }

            if (!ev.RequiresSahfta) p.SAHFTANumber = null;
            if (!ev.RequiresClubName) p.ClubName = null;

            if (!p.InfoAccurateConfirmed)
                ModelState.AddModelError($"{key}.InfoAccurateConfirmed", $"Confirm that {who}'s information is accurate.");

            if (!p.IndemnityAgreed && !p.GuardianIndemnityAgreed)
                ModelState.AddModelError($"{key}.IndemnityAgreed", $"The indemnity must be signed for {who}.");

            if (string.IsNullOrWhiteSpace(p.SocialMediaConsent))
                ModelState.AddModelError($"{key}.SocialMediaConsent", $"Select a social media consent option for {who}.");
        }

        private static readonly string[] SpectatorIrrelevantFields =
            { "GunType", "RifleOwnership", "Division", "OtherDivision", "ShootSelection" };

        // How many problems belong to one person's block, so their card can flag itself and the
        // summary at the top of the form can link straight to it. A booking can run to twelve
        // people, and "something is wrong somewhere below" is not a usable instruction.
        public int ErrorCountFor(int index)
        {
            var prefix = $"Booking.Participants[{index}].";
            return CountErrors(k => k.StartsWith(prefix, StringComparison.Ordinal));
        }

        // The same idea for the parts of the form that belong to nobody in particular, grouped
        // the way the page is laid out so each one can be linked to by its section.
        public int ContactErrorCount => CountErrors(k =>
            k.StartsWith("Booking.", StringComparison.Ordinal) && !IsParticipantKey(k) && !IsMealsKey(k) && !IsPaymentKey(k));

        public int MealsErrorCount => CountErrors(IsMealsKey);

        public int PaymentErrorCount => CountErrors(IsPaymentKey);

        private static bool IsParticipantKey(string k) => k.StartsWith("Booking.Participants[", StringComparison.Ordinal);
        private static bool IsMealsKey(string k) => k.StartsWith("Booking.ExtraMeals", StringComparison.Ordinal);
        private static bool IsPaymentKey(string k) =>
            k.StartsWith("Booking.PaymentMethod", StringComparison.Ordinal) ||
            k.StartsWith("Booking.PaymentReference", StringComparison.Ordinal);

        private int CountErrors(Func<string, bool> match) =>
            ModelState.Where(kv => match(kv.Key)).Sum(kv => kv.Value?.Errors.Count ?? 0);

        private static EventParticipant ToEntity(Event ev, ParticipantInput p, int position) => new()
        {
            Position = position,
            Name = (p.Name ?? "").Trim(),
            Surname = (p.Surname ?? "").Trim(),
            IdNumber = Clean(p.IdNumber),
            AttendanceType = ev.RequiresAttendanceType ? Clean(p.AttendanceType) : null,
            GunType = (p.GunType ?? "").Trim(),
            RifleOwnership = Clean(p.RifleOwnership),
            Division = Clean(p.Division),
            OtherDivision = Clean(p.OtherDivision),
            SAHFTANumber = Clean(p.SAHFTANumber),
            ClubName = Clean(p.ClubName),
            ShootSelection = Clean(p.ShootSelection),
            GuardianName = Clean(p.GuardianName),
            GuardianSurname = Clean(p.GuardianSurname),
            InfoAccurateConfirmed = p.InfoAccurateConfirmed,
            IndemnityAgreed = p.IndemnityAgreed,
            GuardianIndemnityAgreed = p.GuardianIndemnityAgreed,
            SocialMediaConsent = Clean(p.SocialMediaConsent)
        };

        private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private async Task<IActionResult?> StartOnlinePaymentAsync(EventRegistration reg, Event ev, decimal total)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var successUrl = $"{baseUrl}/payment-result?status=success&registrationId={reg.Id}";
            var cancelUrl = $"{baseUrl}/payment-result?status=cancelled&registrationId={reg.Id}";
            var failureUrl = $"{baseUrl}/payment-result?status=failed&registrationId={reg.Id}";

            var gateway = (await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == "PaymentGateway"))?.Value ?? "Ikhokha";

            if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
            {
                var paylink = await _ikhokhaService.CreatePaymentLinkAsync(
                    total,
                    $"{ev.Title} - Registration #{reg.Id}",
                    externalTransactionId: $"EVT-{reg.Id}",
                    requesterUrl: baseUrl,
                    callbackUrl: $"{baseUrl}/api/ikhokha/callback",
                    successPageUrl: successUrl,
                    failurePageUrl: failureUrl,
                    cancelUrl: cancelUrl);

                if (paylink != null && !string.IsNullOrEmpty(paylink.PaylinkUrl))
                {
                    reg.YocoCheckoutId = paylink.PaylinkID;
                    await _context.SaveChangesAsync();
                    return Redirect(paylink.PaylinkUrl);
                }
            }
            else
            {
                var metadata = new Dictionary<string, string>
                {
                    { "eventRegistrationId", reg.Id.ToString() },
                    { "eventId", ev.Id.ToString() }
                };

                var checkout = await _yocoService.CreateCheckoutAsync(
                    total,
                    $"{ev.Title} - Registration #{reg.Id}",
                    successUrl,
                    cancelUrl,
                    failureUrl,
                    metadata);

                if (checkout != null && !string.IsNullOrEmpty(checkout.RedirectUrl))
                {
                    reg.YocoCheckoutId = checkout.Id;
                    await _context.SaveChangesAsync();
                    return Redirect(checkout.RedirectUrl);
                }
            }

            return null;
        }

        private async Task EnrichFromSahftaAsync(EventParticipant p)
        {
            // 1. Try name + surname first (only returns when there's an exact or unambiguous match)
            var api = await _sahftaClient.LookupByNameAsync(p.Name, p.Surname);

            // 2. Fall back to SAHFTA membership number if supplied
            if (api == null &&
                !string.IsNullOrWhiteSpace(p.SAHFTANumber) &&
                !p.SAHFTANumber.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !p.SAHFTANumber.Equals("n/a", StringComparison.OrdinalIgnoreCase) &&
                p.SAHFTANumber != "0")
            {
                api = await _sahftaClient.LookupByMembershipNumberAsync(p.SAHFTANumber);
            }

            if (api == null) return;

            // Overwrite club and membership # with the canonical values from the API.
            if (!string.IsNullOrWhiteSpace(api.club))
                p.ClubName = api.club;
            if (!string.IsNullOrWhiteSpace(api.membershipNumber))
                p.SAHFTANumber = api.membershipNumber;
        }

        private static bool IsOpenForRegistration(Event ev)
        {
            if (!ev.IsRegistrationOpen) return false;
            if (ev.RegistrationCloseDate.HasValue && DateTime.UtcNow.Date > ev.RegistrationCloseDate.Value.Date)
                return false;
            return true;
        }

        private async Task SendInitialEmailsAsync(EventRegistration reg, Event ev, decimal total)
        {
            var breakdown = BuildBreakdownHtml(ev, reg);
            var roster = BuildRosterHtml(ev, reg);
            var paymentInstructions = BuildPaymentInstructions(reg, ev, total);

            var registrantBody = $@"
                <h2>Registration Received</h2>
                <p>Dear {reg.Name},</p>
                <p>Thank you for registering for <strong>{ev.Title}</strong>.</p>
                <p><strong>Event date:</strong> {FormatEventDates(ev)}</p>
                <p><strong>Time:</strong> {ev.Time}</p>
                <p><strong>Location:</strong> {ev.Location}</p>
                {roster}
                {breakdown}
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
                <p><strong>Event:</strong> {ev.Title} ({FormatEventDates(ev)})</p>
                <p><strong>Contact:</strong> {reg.Name} {reg.Surname}</p>
                <p><strong>Email:</strong> {reg.EmailAddress}</p>
                <p><strong>Cell:</strong> {reg.CellNumber}</p>
                <p><strong>ID:</strong> {reg.IdNumber}</p>
                {roster}
                {breakdown}
                <p><strong>Payment method:</strong> {reg.PaymentMethod ?? "N/A"}</p>
                <p><strong>Status:</strong> {reg.Status}</p>";

            var notifyEmail = string.IsNullOrWhiteSpace(ev.NotificationEmail) ? AccountsEmail : ev.NotificationEmail;
            await _emailService.SendEmailAsync(
                notifyEmail,
                $"New Registration - {ev.Title} - #{reg.Id}",
                accountsBody);
        }

        public static string FormatEventDates(Event ev) =>
            ev.IsProvincialTwoDay && ev.EndDate.HasValue
                ? $"{ev.StartDate:dd MMMM yyyy} &ndash; {ev.EndDate.Value:dd MMMM yyyy} (two days)"
                : ev.StartDate.ToString("dd MMMM yyyy");

        private static string BuildRosterHtml(Event ev, EventRegistration reg)
        {
            if (reg.Participants.Count == 0) return "";

            var rows = reg.Participants.OrderBy(p => p.Position).Select(p =>
            {
                var bits = new List<string>();
                if (!string.IsNullOrWhiteSpace(p.AttendanceType)) bits.Add(p.AttendanceType!);
                if (!p.IsSpectator && !string.IsNullOrWhiteSpace(p.GunType)) bits.Add(p.GunType);
                if (!string.IsNullOrWhiteSpace(p.Division)) bits.Add(p.Division!);
                if (!string.IsNullOrWhiteSpace(p.SAHFTANumber)) bits.Add($"SAHFTA {p.SAHFTANumber}");
                if (!string.IsNullOrWhiteSpace(p.ClubName)) bits.Add(p.ClubName!);
                if (ev.IsDoubleHeader && !string.IsNullOrWhiteSpace(p.ShootSelection)) bits.Add(FormatShoot(p.ShootSelection));
                if (string.Equals(p.RifleOwnership, "Club", StringComparison.OrdinalIgnoreCase)) bits.Add("club rifle");

                var detail = bits.Count > 0 ? $" &mdash; {string.Join(" &middot; ", bits)}" : "";
                return $"<li>{p.FullName}{detail}</li>";
            });

            var heading = reg.Participants.Count == 1 ? "Entry" : $"Entries ({reg.Participants.Count} people)";
            return $"<p><strong>{heading}:</strong></p><ul>{string.Join("", rows)}</ul>";
        }

        private static string BuildBreakdownHtml(Event ev, EventRegistration reg)
        {
            var entries = EventFeeCalculator.EntriesTotal(ev, reg.Participants);
            var meals = EventFeeCalculator.MealsTotal(ev, reg.ExtraMeals);
            var total = entries + meals;

            if (total <= 0) return "<p><strong>Total due:</strong> No fee</p>";

            var lines = new List<string>();
            if (entries > 0)
            {
                var suffix = !string.IsNullOrWhiteSpace(ev.EntryFeeDescription) ? $" ({ev.EntryFeeDescription})" : "";
                lines.Add($"<li>Entries: R{entries:F2}{suffix}</li>");
            }
            if (meals > 0)
            {
                var what = string.IsNullOrWhiteSpace(ev.MealDescription) ? "Meals" : ev.MealDescription;
                lines.Add($"<li>{what}: {reg.ExtraMeals} &times; R{ev.MealFee!.Value:F2} = R{meals:F2}</li>");
            }

            return $"<ul>{string.Join("", lines)}</ul><p><strong>Total due:</strong> R{total:F2}</p>";
        }

        private static string FormatShoot(string? sel) => sel switch
        {
            "First" => "Shoot 1 only",
            "Second" => "Shoot 2 only",
            "Both" => "Both shoots",
            _ => sel ?? ""
        };

        private static string BuildPaymentInstructions(EventRegistration reg, Event ev, decimal total)
        {
            if (total <= 0) return "<p>No payment required for this booking.</p>";

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

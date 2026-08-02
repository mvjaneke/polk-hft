using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminEvents
{
    public class RegistrationModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public RegistrationModel(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public EventRegistration Registration { get; private set; } = null!;
        public Event Event { get; private set; } = null!;

        public decimal BookingTotal => EventFeeCalculator.ForBooking(Event, Registration);
        public decimal EntriesTotal => EventFeeCalculator.EntriesTotal(Event, Registration.Participants);
        public decimal MealsTotal => EventFeeCalculator.MealsTotal(Event, Registration.ExtraMeals);

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public int RegId { get; set; }

        // Form-bound editable fields. Kept separate from EventRegistration so a malicious
        // post can't sneak in changes to RegistrationDate, gateway IDs, or indemnity flags.
        [BindProperty]
        public EditViewModel Edit { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (!await LoadAsync()) return NotFound();

            Edit = EditViewModel.From(Registration);
            return Page();
        }

        public async Task<IActionResult> OnPostAddPersonAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (!await LoadAsync()) return NotFound();

            Edit.Participants.Add(new ParticipantEdit());
            ModelState.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostRemovePersonAsync(int index)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (!await LoadAsync()) return NotFound();

            if (index >= 0 && index < Edit.Participants.Count)
                Edit.Participants.RemoveAt(index);

            ModelState.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (!await LoadAsync()) return NotFound();

            if (string.IsNullOrWhiteSpace(Edit.Name))
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.Name)}", "First name is required.");
            if (string.IsNullOrWhiteSpace(Edit.Surname))
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.Surname)}", "Surname is required.");
            if (string.IsNullOrWhiteSpace(Edit.EmailAddress))
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.EmailAddress)}", "Email is required.");
            if (string.IsNullOrWhiteSpace(Edit.CellNumber))
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.CellNumber)}", "Cell number is required.");
            if (string.IsNullOrWhiteSpace(Edit.IdNumber))
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.IdNumber)}", "ID number is required.");

            if (Edit.AmountPaid < 0)
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.AmountPaid)}", "Amount paid cannot be negative.");
            if (Edit.ExtraMeals < 0)
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.ExtraMeals)}", "Meals cannot be negative.");

            if (Edit.Participants.Count == 0)
                ModelState.AddModelError(string.Empty, "A booking needs at least one person on it.");

            for (int i = 0; i < Edit.Participants.Count; i++)
            {
                var p = Edit.Participants[i];
                var key = $"{nameof(Edit)}.{nameof(Edit.Participants)}[{i}]";

                if (string.IsNullOrWhiteSpace(p.Name))
                    ModelState.AddModelError($"{key}.{nameof(p.Name)}", "First name is required.");
                if (string.IsNullOrWhiteSpace(p.Surname))
                    ModelState.AddModelError($"{key}.{nameof(p.Surname)}", "Surname is required.");

                var spectator = string.Equals(p.AttendanceType, "Spectator", StringComparison.OrdinalIgnoreCase);
                if (spectator)
                {
                    p.GunType = "N/A";
                    p.RifleOwnership = null;
                    p.Division = null;
                    p.OtherDivision = null;
                    p.ShootSelection = null;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(p.GunType))
                        ModelState.AddModelError($"{key}.{nameof(p.GunType)}", "Gun type is required.");

                    if (Event.IsDoubleHeader)
                    {
                        var sel = p.ShootSelection;
                        if (sel != "First" && sel != "Second" && sel != "Both")
                            ModelState.AddModelError($"{key}.{nameof(p.ShootSelection)}", "Choose which shoot(s) this person will enter.");
                    }
                    else
                    {
                        p.ShootSelection = null;
                    }
                }
            }

            if (!ModelState.IsValid) return Page();

            Edit.ApplyTo(Registration, _context);
            await _context.SaveChangesAsync();

            TempData["SaveMessage"] = "Registration details updated.";
            return RedirectToPage(new { id = Id, regId = RegId });
        }

        public async Task<IActionResult> OnPostMarkPaidAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (!await LoadAsync()) return NotFound();

            if (Registration.Status != "Paid")
            {
                Registration.Status = "Paid";
                if (Registration.AmountPaid == 0)
                    Registration.AmountPaid = EventFeeCalculator.ForBooking(Event, Registration);
                await _context.SaveChangesAsync();

                await EventEmailTemplates.SendEventPaymentConfirmedAsync(_emailService, Registration, Event);
            }
            return RedirectToPage(new { id = Id, regId = RegId });
        }

        public async Task<IActionResult> OnPostCancelAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations.FindAsync(RegId);
            if (reg != null)
            {
                reg.Status = "Cancelled";
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { id = Id, regId = RegId });
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations.FindAsync(RegId);
            if (reg != null)
            {
                // Participants cascade with the booking.
                _context.EventRegistrations.Remove(reg);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage("Manage", new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.Event)
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.Id == RegId && r.EventId == Id);
            if (reg == null) return false;

            Registration = reg;
            Event = reg.Event;
            return true;
        }

        public class EditViewModel
        {
            public string Name { get; set; } = string.Empty;
            public string Surname { get; set; } = string.Empty;
            public string EmailAddress { get; set; } = string.Empty;
            public string CellNumber { get; set; } = string.Empty;
            public string IdNumber { get; set; } = string.Empty;

            public int ExtraMeals { get; set; }

            public string? PaymentMethod { get; set; }
            public decimal AmountPaid { get; set; }
            public string? PaymentReference { get; set; }

            public List<ParticipantEdit> Participants { get; set; } = new();

            public static EditViewModel From(EventRegistration r) => new()
            {
                Name = r.Name,
                Surname = r.Surname,
                EmailAddress = r.EmailAddress,
                CellNumber = r.CellNumber,
                IdNumber = r.IdNumber,
                ExtraMeals = r.ExtraMeals,
                PaymentMethod = r.PaymentMethod,
                AmountPaid = r.AmountPaid,
                PaymentReference = r.PaymentReference,
                Participants = r.Participants
                    .OrderBy(p => p.Position)
                    .Select(ParticipantEdit.From)
                    .ToList()
            };

            // Applies the posted values, then reconciles the participant rows: existing rows
            // are updated by id, rows the admin removed are deleted, and rows with id 0 are
            // added. Positions are renumbered to match the form order.
            public void ApplyTo(EventRegistration r, ApplicationDbContext context)
            {
                r.Name = Name.Trim();
                r.Surname = Surname.Trim();
                r.EmailAddress = EmailAddress.Trim();
                r.CellNumber = CellNumber.Trim();
                r.IdNumber = IdNumber.Trim();
                r.ExtraMeals = ExtraMeals;
                r.PaymentMethod = string.IsNullOrWhiteSpace(PaymentMethod) ? null : PaymentMethod.Trim();
                r.AmountPaid = AmountPaid;
                r.PaymentReference = string.IsNullOrWhiteSpace(PaymentReference) ? null : PaymentReference.Trim();

                var keptIds = Participants.Where(p => p.Id != 0).Select(p => p.Id).ToHashSet();
                foreach (var gone in r.Participants.Where(p => !keptIds.Contains(p.Id)).ToList())
                {
                    r.Participants.Remove(gone);
                    context.EventParticipants.Remove(gone);
                }

                for (int i = 0; i < Participants.Count; i++)
                {
                    var input = Participants[i];
                    var entity = input.Id != 0
                        ? r.Participants.FirstOrDefault(p => p.Id == input.Id)
                        : null;

                    if (entity == null)
                    {
                        entity = new EventParticipant { EventRegistrationId = r.Id };
                        r.Participants.Add(entity);
                    }

                    input.ApplyTo(entity, i + 1);
                }
            }
        }

        public class ParticipantEdit
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
            public string Surname { get; set; } = string.Empty;
            public string? IdNumber { get; set; }
            public string? AttendanceType { get; set; }
            public string GunType { get; set; } = string.Empty;
            public string? RifleOwnership { get; set; }
            public string? Division { get; set; }
            public string? OtherDivision { get; set; }
            public string? SAHFTANumber { get; set; }
            public string? ClubName { get; set; }
            public string? ShootSelection { get; set; }
            public string? GuardianName { get; set; }
            public string? GuardianSurname { get; set; }
            public string? SocialMediaConsent { get; set; }
            public int? StartingLaneShoot1 { get; set; }
            public int? StartingLaneShoot2 { get; set; }

            // Signed by the registrant, shown read-only on the form.
            public bool InfoAccurateConfirmed { get; set; }
            public bool IndemnityAgreed { get; set; }
            public bool GuardianIndemnityAgreed { get; set; }

            public static ParticipantEdit From(EventParticipant p) => new()
            {
                Id = p.Id,
                Name = p.Name,
                Surname = p.Surname,
                IdNumber = p.IdNumber,
                AttendanceType = p.AttendanceType,
                GunType = p.GunType,
                RifleOwnership = p.RifleOwnership,
                Division = p.Division,
                OtherDivision = p.OtherDivision,
                SAHFTANumber = p.SAHFTANumber,
                ClubName = p.ClubName,
                ShootSelection = p.ShootSelection,
                GuardianName = p.GuardianName,
                GuardianSurname = p.GuardianSurname,
                SocialMediaConsent = p.SocialMediaConsent,
                StartingLaneShoot1 = p.StartingLaneShoot1,
                StartingLaneShoot2 = p.StartingLaneShoot2,
                InfoAccurateConfirmed = p.InfoAccurateConfirmed,
                IndemnityAgreed = p.IndemnityAgreed,
                GuardianIndemnityAgreed = p.GuardianIndemnityAgreed
            };

            public void ApplyTo(EventParticipant p, int position)
            {
                p.Position = position;
                p.Name = Name.Trim();
                p.Surname = Surname.Trim();
                p.IdNumber = Clean(IdNumber);
                p.AttendanceType = Clean(AttendanceType);
                p.GunType = (GunType ?? "").Trim();
                p.RifleOwnership = Clean(RifleOwnership);
                p.Division = Clean(Division);
                p.OtherDivision = Clean(OtherDivision);
                p.SAHFTANumber = Clean(SAHFTANumber);
                p.ClubName = Clean(ClubName);
                p.ShootSelection = Clean(ShootSelection);
                p.GuardianName = Clean(GuardianName);
                p.GuardianSurname = Clean(GuardianSurname);
                p.SocialMediaConsent = Clean(SocialMediaConsent);
                p.StartingLaneShoot1 = StartingLaneShoot1;
                p.StartingLaneShoot2 = StartingLaneShoot2;
                // Indemnity flags are never edited by the admin — they're the registrant's
                // signature. New people added here start unsigned, which is visible on the form.
            }

            private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }
    }
}

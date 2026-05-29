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

            var reg = await _context.EventRegistrations
                .Include(r => r.Event)
                .FirstOrDefaultAsync(r => r.Id == RegId && r.EventId == Id);
            if (reg == null) return NotFound();

            Registration = reg;
            Event = reg.Event;
            Edit = EditViewModel.From(reg);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations
                .Include(r => r.Event)
                .FirstOrDefaultAsync(r => r.Id == RegId && r.EventId == Id);
            if (reg == null) return NotFound();

            Registration = reg;
            Event = reg.Event;

            // Per-event optional fields — only validate what this event actually uses.
            if (!Event.RequiresAttendanceType) ModelState.Remove(nameof(Edit) + "." + nameof(Edit.AttendanceType));
            if (!Event.RequiresSahfta) ModelState.Remove(nameof(Edit) + "." + nameof(Edit.SAHFTANumber));
            if (!Event.RequiresClubName) ModelState.Remove(nameof(Edit) + "." + nameof(Edit.ClubName));
            if (!Event.RequiresDivision) ModelState.Remove(nameof(Edit) + "." + nameof(Edit.Division));
            if (!Event.AllowsClubRifle) ModelState.Remove(nameof(Edit) + "." + nameof(Edit.RifleOwnership));
            if (!Event.IsDoubleHeader) ModelState.Remove(nameof(Edit) + "." + nameof(Edit.ShootSelection));

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
            if (string.IsNullOrWhiteSpace(Edit.GunType))
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.GunType)}", "Gun type is required.");

            if (Event.IsDoubleHeader)
            {
                var sel = Edit.ShootSelection;
                if (sel != "First" && sel != "Second" && sel != "Both")
                    ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.ShootSelection)}", "Please choose which shoot(s) the competitor will enter.");
            }
            else
            {
                Edit.ShootSelection = null;
            }

            if (Edit.AmountPaid < 0)
                ModelState.AddModelError($"{nameof(Edit)}.{nameof(Edit.AmountPaid)}", "Amount paid cannot be negative.");

            if (!ModelState.IsValid)
            {
                Registration = reg;
                return Page();
            }

            Edit.ApplyTo(reg);
            await _context.SaveChangesAsync();

            TempData["SaveMessage"] = "Registration details updated.";
            return RedirectToPage(new { id = Id, regId = RegId });
        }

        public async Task<IActionResult> OnPostMarkPaidAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations.Include(r => r.Event).FirstOrDefaultAsync(r => r.Id == RegId);
            if (reg != null && reg.Status != "Paid")
            {
                reg.Status = "Paid";
                if (reg.Event?.EntryFee.HasValue == true && reg.AmountPaid == 0)
                    reg.AmountPaid = reg.Event.EntryFee.Value;
                await _context.SaveChangesAsync();

                if (reg.Event != null)
                    await EventEmailTemplates.SendEventPaymentConfirmedAsync(_emailService, reg, reg.Event);
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
                _context.EventRegistrations.Remove(reg);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage("Manage", new { id = Id });
        }

        public class EditViewModel
        {
            public string Name { get; set; } = string.Empty;
            public string Surname { get; set; } = string.Empty;
            public string EmailAddress { get; set; } = string.Empty;
            public string CellNumber { get; set; } = string.Empty;
            public string IdNumber { get; set; } = string.Empty;

            public string? AttendanceType { get; set; }
            public string GunType { get; set; } = string.Empty;
            public string? RifleOwnership { get; set; }
            public string? Division { get; set; }
            public string? OtherDivision { get; set; }
            public string? ShootSelection { get; set; }
            public string? SAHFTANumber { get; set; }
            public string? ClubName { get; set; }

            public string? GuardianName { get; set; }
            public string? GuardianSurname { get; set; }

            public string? SocialMediaConsent { get; set; }

            public string? PaymentMethod { get; set; }
            public decimal AmountPaid { get; set; }
            public string? PaymentReference { get; set; }

            public static EditViewModel From(EventRegistration r) => new()
            {
                Name = r.Name,
                Surname = r.Surname,
                EmailAddress = r.EmailAddress,
                CellNumber = r.CellNumber,
                IdNumber = r.IdNumber,
                AttendanceType = r.AttendanceType,
                GunType = r.GunType,
                RifleOwnership = r.RifleOwnership,
                Division = r.Division,
                OtherDivision = r.OtherDivision,
                ShootSelection = r.ShootSelection,
                SAHFTANumber = r.SAHFTANumber,
                ClubName = r.ClubName,
                GuardianName = r.GuardianName,
                GuardianSurname = r.GuardianSurname,
                SocialMediaConsent = r.SocialMediaConsent,
                PaymentMethod = r.PaymentMethod,
                AmountPaid = r.AmountPaid,
                PaymentReference = r.PaymentReference
            };

            public void ApplyTo(EventRegistration r)
            {
                r.Name = Name.Trim();
                r.Surname = Surname.Trim();
                r.EmailAddress = EmailAddress.Trim();
                r.CellNumber = CellNumber.Trim();
                r.IdNumber = IdNumber.Trim();
                r.AttendanceType = string.IsNullOrWhiteSpace(AttendanceType) ? null : AttendanceType.Trim();
                r.GunType = GunType.Trim();
                r.RifleOwnership = string.IsNullOrWhiteSpace(RifleOwnership) ? null : RifleOwnership.Trim();
                r.Division = string.IsNullOrWhiteSpace(Division) ? null : Division.Trim();
                r.OtherDivision = string.IsNullOrWhiteSpace(OtherDivision) ? null : OtherDivision.Trim();
                r.ShootSelection = string.IsNullOrWhiteSpace(ShootSelection) ? null : ShootSelection.Trim();
                r.SAHFTANumber = string.IsNullOrWhiteSpace(SAHFTANumber) ? null : SAHFTANumber.Trim();
                r.ClubName = string.IsNullOrWhiteSpace(ClubName) ? null : ClubName.Trim();
                r.GuardianName = string.IsNullOrWhiteSpace(GuardianName) ? null : GuardianName.Trim();
                r.GuardianSurname = string.IsNullOrWhiteSpace(GuardianSurname) ? null : GuardianSurname.Trim();
                r.SocialMediaConsent = string.IsNullOrWhiteSpace(SocialMediaConsent) ? null : SocialMediaConsent.Trim();
                r.PaymentMethod = string.IsNullOrWhiteSpace(PaymentMethod) ? null : PaymentMethod.Trim();
                r.AmountPaid = AmountPaid;
                r.PaymentReference = string.IsNullOrWhiteSpace(PaymentReference) ? null : PaymentReference.Trim();
            }
        }
    }
}

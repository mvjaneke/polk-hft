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
            return Page();
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
    }
}

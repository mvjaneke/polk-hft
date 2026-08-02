using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Added this line

namespace POLK_DOTNET.Pages
{
    public class RegistrationConfirmationModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public RegistrationConfirmationModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public EventRegistration EventRegistration { get; set; } = null!;
        public Event Event { get; set; } = null!;

        public decimal EntriesTotal { get; private set; }
        public decimal MealsTotal { get; private set; }
        public decimal Total => EntriesTotal + MealsTotal;

        // Set when the registrant chose to pay online but the gateway never returned a
        // payment link, so they were redirected here instead of to the payment page.
        public bool PaymentStartFailed { get; private set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            EventRegistration = await _context.EventRegistrations
                .Include(er => er.Event)
                .Include(er => er.Participants)
                .FirstOrDefaultAsync(er => er.Id == id);

            if (EventRegistration == null)
            {
                return NotFound();
            }

            Event = EventRegistration.Event;
            EntriesTotal = EventFeeCalculator.EntriesTotal(Event, EventRegistration.Participants);
            MealsTotal = EventFeeCalculator.MealsTotal(Event, EventRegistration.ExtraMeals);
            PaymentStartFailed = TempData["PaymentStartFailed"] as string == "true";

            return Page();
        }
    }
}

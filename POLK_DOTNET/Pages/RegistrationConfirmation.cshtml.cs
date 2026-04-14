using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using POLK_DOTNET.Data;
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

        public async Task<IActionResult> OnGetAsync(int id)
        {
            EventRegistration = await _context.EventRegistrations
                .Include(er => er.Event)
                .FirstOrDefaultAsync(er => er.Id == id);

            if (EventRegistration == null)
            {
                return NotFound();
            }

            Event = EventRegistration.Event;

            return Page();
        }
    }
}

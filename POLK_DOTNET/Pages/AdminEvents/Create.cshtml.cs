using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Pages.AdminEvents
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context) { _context = context; }

        [BindProperty]
        public Event Event { get; set; } = new Event { EventType = "HFT", StartDate = DateTime.Today };

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (!ModelState.IsValid)
                return Page();

            _context.Events.Add(Event);
            await _context.SaveChangesAsync();

            return RedirectToPage("Manage", new { id = Event.Id });
        }
    }
}

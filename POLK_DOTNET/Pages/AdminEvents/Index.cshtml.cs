using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Pages.AdminEvents
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context) { _context = context; }

        public List<EventRow> Rows { get; private set; } = new();

        public class EventRow
        {
            public Event Event { get; set; } = null!;
            public int Total { get; set; }
            public int Paid { get; set; }
            public int Pending { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var events = await _context.Events.OrderByDescending(e => e.StartDate).ToListAsync();
            var regs = await _context.EventRegistrations
                .GroupBy(r => r.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    Total = g.Count(),
                    Paid = g.Count(r => r.Status == "Paid"),
                    Pending = g.Count(r => r.Status == "Pending")
                })
                .ToListAsync();

            Rows = events.Select(e =>
            {
                var s = regs.FirstOrDefault(x => x.EventId == e.Id);
                return new EventRow
                {
                    Event = e,
                    Total = s?.Total ?? 0,
                    Paid = s?.Paid ?? 0,
                    Pending = s?.Pending ?? 0
                };
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostToggleRegistrationAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(id);
            if (ev != null)
            {
                ev.IsRegistrationOpen = !ev.IsRegistrationOpen;
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(id);
            if (ev != null)
            {
                _context.Events.Remove(ev);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}

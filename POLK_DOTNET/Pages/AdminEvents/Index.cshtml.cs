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
            public int People { get; set; }
            public int Paid { get; set; }
            public int Pending { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var events = await _context.Events.OrderByDescending(e => e.StartDate).ToListAsync();
            // Cancelled bookings are left out, and people are counted as well as bookings —
            // one booking can now cover a whole party, so a booking count alone says nothing
            // about how many bodies turn up.
            //
            // The head count is projected per booking and grouped in memory rather than summed
            // inside a GroupBy: a per-row Participants.Count is an ordinary correlated subquery
            // that always translates, whereas aggregating a collection navigation across a group
            // is not guaranteed to, and this page only runs behind the admin login.
            var regs = (await _context.EventRegistrations
                    .Where(r => r.Status != "Cancelled")
                    .Select(r => new { r.EventId, r.Status, People = r.Participants.Count })
                    .ToListAsync())
                .GroupBy(x => x.EventId)
                .Select(g => new
                {
                    EventId = g.Key,
                    Total = g.Count(),
                    People = g.Sum(x => x.People),
                    Paid = g.Count(x => x.Status == "Paid"),
                    Pending = g.Count(x => x.Status == "Pending")
                })
                .ToList();

            Rows = events.Select(e =>
            {
                var s = regs.FirstOrDefault(x => x.EventId == e.Id);
                return new EventRow
                {
                    Event = e,
                    Total = s?.Total ?? 0,
                    People = s?.People ?? 0,
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

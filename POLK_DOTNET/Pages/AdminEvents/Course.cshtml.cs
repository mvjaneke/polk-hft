using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminEvents
{
    public class CourseModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CourseModel(ApplicationDbContext context) { _context = context; }

        public Event Event { get; private set; } = null!;

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Shoot { get; set; } = 1;

        [BindProperty]
        public List<CourseTargetInput> Targets { get; set; } = new();

        public Dictionary<int, TroyerCalculator.RowResult> RowResults { get; private set; } = new();
        public TroyerCalculator.SummaryResult? Summary { get; private set; }

        public bool ShowShootTabs => Event.IsDoubleHeader && !Event.UseSameCourseForBothShoots;
        public int CurrentTargetCount => Targets.Count;
        public bool CurrentShootConfigured => Targets.Count > 0;

        public class CourseTargetInput
        {
            public int Id { get; set; }
            public int TargetNumber { get; set; }
            public int Lane { get; set; }
            public string? Posture { get; set; }
            public bool IsInclineDecline { get; set; }
            public bool IsShaded { get; set; }
            public int KillZoneMm { get; set; }
            public decimal DistanceMeters { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();
            Event = ev;

            NormalizeShoot(ev);

            await LoadOrInitializeAsync(ev);
            ComputeResults();
            return Page();
        }

        public async Task<IActionResult> OnPostSetTargetCountAsync(int targetCount)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            if (targetCount != 30 && targetCount != 40)
                return BadRequest();

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            NormalizeShoot(ev);

            // Event.CourseTargetCount tracks the primary (Shoot 1) count. Don't overwrite for Shoot 2.
            if (Shoot == 1)
                ev.CourseTargetCount = targetCount;

            // Reset only rows for the current shoot
            var existing = await _context.CourseTargets.Where(c => c.EventId == Id && c.Shoot == Shoot).ToListAsync();
            if (existing.Count != targetCount)
            {
                _context.CourseTargets.RemoveRange(existing);
                for (int i = 1; i <= targetCount; i++)
                {
                    _context.CourseTargets.Add(new CourseTarget
                    {
                        EventId = Id,
                        Shoot = Shoot,
                        TargetNumber = i,
                        Lane = (i + 1) / 2,
                        Posture = "Prone",
                        KillZoneMm = 0,
                        DistanceMeters = 0
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = Id, shoot = Shoot });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();
            Event = ev;

            NormalizeShoot(ev);

            var dbRows = await _context.CourseTargets
                .Where(c => c.EventId == Id && c.Shoot == Shoot)
                .ToListAsync();

            foreach (var input in Targets)
            {
                var row = dbRows.FirstOrDefault(r => r.Id == input.Id);
                if (row == null) continue;
                row.Posture = string.IsNullOrWhiteSpace(input.Posture) ? "Prone" : input.Posture;
                row.IsInclineDecline = input.IsInclineDecline;
                row.IsShaded = input.IsShaded;
                row.KillZoneMm = input.KillZoneMm;
                row.DistanceMeters = input.DistanceMeters;
                row.Lane = input.Lane > 0 ? input.Lane : (input.TargetNumber + 1) / 2;
            }

            await _context.SaveChangesAsync();

            TempData["CourseSaved"] = "Course saved.";
            return RedirectToPage(new { id = Id, shoot = Shoot });
        }

        private void NormalizeShoot(Event ev)
        {
            // Force Shoot=1 when there's no second shoot to configure
            if (!ev.IsDoubleHeader || ev.UseSameCourseForBothShoots) Shoot = 1;
            if (Shoot != 1 && Shoot != 2) Shoot = 1;
        }

        private async Task LoadOrInitializeAsync(Event ev)
        {
            var rows = await _context.CourseTargets
                .Where(c => c.EventId == ev.Id && c.Shoot == Shoot)
                .OrderBy(c => c.TargetNumber)
                .ToListAsync();

            Targets = rows.Select(r => new CourseTargetInput
            {
                Id = r.Id,
                TargetNumber = r.TargetNumber,
                Lane = r.Lane,
                Posture = r.Posture,
                IsInclineDecline = r.IsInclineDecline,
                IsShaded = r.IsShaded,
                KillZoneMm = r.KillZoneMm,
                DistanceMeters = r.DistanceMeters
            }).ToList();
        }

        private void ComputeResults()
        {
            if (Targets.Count == 0) return;

            var entities = Targets.Select(i => new CourseTarget
            {
                Id = i.Id,
                TargetNumber = i.TargetNumber,
                Lane = i.Lane,
                Posture = i.Posture,
                IsInclineDecline = i.IsInclineDecline,
                IsShaded = i.IsShaded,
                KillZoneMm = i.KillZoneMm,
                DistanceMeters = i.DistanceMeters
            }).ToList();

            foreach (var e in entities)
                RowResults[e.Id] = TroyerCalculator.ComputeRow(e);

            Summary = TroyerCalculator.ComputeSummary(entities, Targets.Count);
        }
    }
}

using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminEvents
{
    public class ManageModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ScorecardPdfService _scorecardService;
        private readonly SahftaMembersClient _sahftaClient;
        private readonly EmailService _emailService;

        public ManageModel(ApplicationDbContext context, ScorecardPdfService scorecardService, SahftaMembersClient sahftaClient, EmailService emailService)
        {
            _context = context;
            _scorecardService = scorecardService;
            _sahftaClient = sahftaClient;
            _emailService = emailService;
        }

        public Event Event { get; private set; } = null!;
        public List<EventRegistration> Registrations { get; private set; } = new();

        public int TotalCount { get; private set; }
        public int PaidCount { get; private set; }
        public int PendingCount { get; private set; }
        public int ConfirmedCount { get; private set; }
        public int CancelledCount { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();
            Event = ev;

            await LoadStatsAsync();
            await LoadRegistrationsAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetSampleScorecardAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var course = await _context.CourseTargets
                .Where(c => c.EventId == Id)
                .OrderBy(c => c.TargetNumber)
                .ToListAsync();

            var sample = new ScorecardPdfService.ParticipantInfo
            {
                Name = "Sample",
                Surname = "Shooter",
                Club = "POLK",
                MembershipNumber = "19-0000",
                Division = "Open",
                GunType = "PCP"
            };

            var pdf = _scorecardService.GenerateBatch(ev, course, new[] { sample });
            return File(pdf, "application/pdf", $"scorecard_sample_{ev.Id}.pdf");
        }

        public async Task<IActionResult> OnGetGenerateScorecardsAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var course = await _context.CourseTargets
                .Where(c => c.EventId == Id)
                .OrderBy(c => c.TargetNumber)
                .ToListAsync();

            var regs = await _context.EventRegistrations
                .Where(r => r.EventId == Id && r.Status != "Cancelled")
                .OrderBy(r => r.Surname).ThenBy(r => r.Name)
                .ToListAsync();

            var participants = new List<ScorecardPdfService.ParticipantInfo>();
            foreach (var reg in regs)
            {
                participants.Add(await EnrichAsync(reg));
            }

            // 6 blank cards for walk-ins
            for (int i = 0; i < 6; i++)
                participants.Add(new ScorecardPdfService.ParticipantInfo());

            var pdf = _scorecardService.GenerateBatch(ev, course, participants);
            var safeTitle = string.Concat((ev.Title ?? "event").Split(Path.GetInvalidFileNameChars()));
            return File(pdf, "application/pdf", $"scorecards_{safeTitle}_{ev.Id}.pdf");
        }

        private async Task<ScorecardPdfService.ParticipantInfo> EnrichAsync(EventRegistration reg)
        {
            // 1. If SAHFTA # supplied → exact lookup
            SahftaMembersClient.MemberDto? api = null;
            if (!string.IsNullOrWhiteSpace(reg.SAHFTANumber) &&
                !reg.SAHFTANumber.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !reg.SAHFTANumber.Equals("n/a", StringComparison.OrdinalIgnoreCase) &&
                reg.SAHFTANumber != "0")
            {
                api = await _sahftaClient.LookupByMembershipNumberAsync(reg.SAHFTANumber);
            }

            // 2. Fall back to name lookup
            if (api == null)
            {
                api = await _sahftaClient.LookupByNameAsync(reg.Name, reg.Surname);
            }

            if (api != null)
            {
                return new ScorecardPdfService.ParticipantInfo
                {
                    Name = api.firstName ?? reg.Name,
                    Surname = api.surname ?? reg.Surname,
                    Club = api.club ?? (reg.ClubName ?? ""),
                    MembershipNumber = api.membershipNumber ?? (reg.SAHFTANumber ?? ""),
                    Division = api.leaderboardDivision ?? reg.Division,
                    GunType = reg.GunType
                };
            }

            // No API match → use registration, mark club/membership N/A per your rule
            return new ScorecardPdfService.ParticipantInfo
            {
                Name = reg.Name,
                Surname = reg.Surname,
                Club = "N/A",
                MembershipNumber = "N/A",
                Division = reg.Division,
                GunType = reg.GunType
            };
        }

        public async Task<IActionResult> OnGetSampleScorecardUnevenAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var targetCount = ev.CourseTargetCount ?? 40;

            // Synthetic uneven lane layout — varied target counts per lane
            int[] laneSizes = targetCount == 30
                ? new[] { 2, 3, 1, 2, 2, 3, 1, 2, 2, 2, 3, 1, 2, 2, 2 }  // sums to 30
                : new[] { 2, 3, 1, 2, 2, 3, 1, 2, 2, 2, 3, 1, 2, 2, 2, 3, 1, 2, 2, 2 }; // 40

            var fake = new List<CourseTarget>();
            int targetNo = 1;
            int laneNo = 1;
            string[] postures = { "UnStand", "UnKneel", "SupStand", "SupKneel" };
            foreach (var size in laneSizes)
            {
                for (int i = 0; i < size && targetNo <= targetCount; i++)
                {
                    fake.Add(new CourseTarget
                    {
                        EventId = ev.Id,
                        TargetNumber = targetNo,
                        Lane = laneNo,
                        Posture = postures[(targetNo - 1) % postures.Length],
                        KillZoneMm = 25,
                        DistanceMeters = 20
                    });
                    targetNo++;
                }
                laneNo++;
            }

            var sample = new ScorecardPdfService.ParticipantInfo
            {
                Name = "Uneven",
                Surname = "Layout",
                Club = "POLK",
                MembershipNumber = "19-0000",
                Division = "Open",
                GunType = "PCP"
            };

            var pdf = _scorecardService.GenerateBatch(ev, fake, new[] { sample });
            return File(pdf, "application/pdf", $"scorecard_sample_uneven_{ev.Id}.pdf");
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var regs = await _context.EventRegistrations
                .Where(r => r.EventId == Id)
                .OrderBy(r => r.RegistrationDate)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,Date,Name,Surname,Email,Cell,ID Number,Attendance,GunType,Rifle,Division,OtherDivision,SAHFTA,Club,GuardianName,GuardianSurname,PaymentMethod,Status,AmountPaid,Reference,IndemnityAgreed,GuardianIndemnity,SocialMediaConsent");
            foreach (var r in regs)
            {
                sb.Append(r.Id).Append(',')
                  .Append(r.RegistrationDate.ToString("yyyy-MM-dd HH:mm")).Append(',')
                  .Append(Csv(r.Name)).Append(',')
                  .Append(Csv(r.Surname)).Append(',')
                  .Append(Csv(r.EmailAddress)).Append(',')
                  .Append(Csv(r.CellNumber)).Append(',')
                  .Append(Csv(r.IdNumber)).Append(',')
                  .Append(Csv(r.AttendanceType)).Append(',')
                  .Append(Csv(r.GunType)).Append(',')
                  .Append(Csv(r.RifleOwnership)).Append(',')
                  .Append(Csv(r.Division)).Append(',')
                  .Append(Csv(r.OtherDivision)).Append(',')
                  .Append(Csv(r.SAHFTANumber)).Append(',')
                  .Append(Csv(r.ClubName)).Append(',')
                  .Append(Csv(r.GuardianName)).Append(',')
                  .Append(Csv(r.GuardianSurname)).Append(',')
                  .Append(Csv(r.PaymentMethod)).Append(',')
                  .Append(Csv(r.Status)).Append(',')
                  .Append(r.AmountPaid.ToString("F2")).Append(',')
                  .Append(Csv(r.PaymentReference)).Append(',')
                  .Append(r.IndemnityAgreed).Append(',')
                  .Append(r.GuardianIndemnityAgreed).Append(',')
                  .Append(Csv(r.SocialMediaConsent))
                  .AppendLine();
            }

            var safeTitle = string.Concat(ev.Title.Split(Path.GetInvalidFileNameChars()));
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"registrations_{safeTitle}_{ev.Id}.csv");
        }

        public async Task<IActionResult> OnPostMarkPaidAsync(int registrationId)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations.Include(r => r.Event).FirstOrDefaultAsync(r => r.Id == registrationId);
            if (reg != null && reg.Status != "Paid")
            {
                reg.Status = "Paid";
                if (reg.Event?.EntryFee.HasValue == true && reg.AmountPaid == 0)
                    reg.AmountPaid = reg.Event.EntryFee.Value;
                await _context.SaveChangesAsync();

                if (reg.Event != null)
                    await EventEmailTemplates.SendEventPaymentConfirmedAsync(_emailService, reg, reg.Event);
            }
            return RedirectToPage(new { id = Id, Search, StatusFilter });
        }

        public async Task<IActionResult> OnPostCancelAsync(int registrationId)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations.FindAsync(registrationId);
            if (reg != null)
            {
                reg.Status = "Cancelled";
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { id = Id, Search, StatusFilter });
        }

        public async Task<IActionResult> OnPostToggleRegistrationAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev != null)
            {
                ev.IsRegistrationOpen = !ev.IsRegistrationOpen;
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { id = Id });
        }

        private async Task LoadStatsAsync()
        {
            var all = await _context.EventRegistrations.Where(r => r.EventId == Id).ToListAsync();
            TotalCount = all.Count;
            PaidCount = all.Count(r => r.Status == "Paid");
            PendingCount = all.Count(r => r.Status == "Pending");
            ConfirmedCount = all.Count(r => r.Status == "Confirmed");
            CancelledCount = all.Count(r => r.Status == "Cancelled");
        }

        private async Task LoadRegistrationsAsync()
        {
            var q = _context.EventRegistrations.Where(r => r.EventId == Id);

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(r =>
                    r.Name.Contains(s) ||
                    r.Surname.Contains(s) ||
                    r.EmailAddress.Contains(s) ||
                    r.CellNumber.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
                q = q.Where(r => r.Status == StatusFilter);

            Registrations = await q.OrderByDescending(r => r.RegistrationDate).ToListAsync();
        }

        private static string Csv(string? v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            var needsQuote = v.Contains(',') || v.Contains('"') || v.Contains('\n');
            var escaped = v.Replace("\"", "\"\"");
            return needsQuote ? $"\"{escaped}\"" : escaped;
        }
    }
}

using System.Globalization;
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
        private readonly ExcelExportService _excelService;
        private readonly SahftaMembersClient _sahftaClient;
        private readonly EmailService _emailService;

        public ManageModel(ApplicationDbContext context, ScorecardPdfService scorecardService, ExcelExportService excelService, SahftaMembersClient sahftaClient, EmailService emailService)
        {
            _context = context;
            _scorecardService = scorecardService;
            _excelService = excelService;
            _sahftaClient = sahftaClient;
            _emailService = emailService;
        }

        public Event Event { get; private set; } = null!;

        // Bookings, each with its people loaded. One booking can cover several shooters.
        public List<EventRegistration> Registrations { get; private set; } = new();

        // Whether a Troyer course actually has target rows for each round. Drives the
        // scorecard/score-sheet buttons. Based on real CourseTargets rows rather than
        // Event.CourseTargetCount, which is a round-1-only flag that an event edit can wipe.
        public bool Shoot1Configured { get; private set; }
        public bool Shoot2Configured { get; private set; }

        // Round 2 reuses the round-1 course when "use same course for both" is on.
        public bool Shoot1Ready => Shoot1Configured;
        public bool Shoot2Ready => Event.UseSameCourseForBothShoots ? Shoot1Configured : Shoot2Configured;
        public bool CourseConfigured => Shoot1Ready || Shoot2Ready;

        // Everything on this dashboard counts live bookings — every booking that has not been
        // cancelled, paid or not. Cancelled is reported on its own and is in no other figure,
        // so Paid + Pending + Confirmed adds up to BookingCount instead of the tiles quietly
        // counting different populations.
        public int BookingCount { get; private set; }
        public int PaidCount { get; private set; }
        public int PendingCount { get; private set; }
        public int ConfirmedCount { get; private set; }
        public int CancelledCount { get; private set; }
        public int OtherStatusCount { get; private set; }

        // Head counts across live bookings. "Entries" is how many were registered; "unique" is
        // how many people that looks like once repeats are taken out. They differ when somebody
        // registers twice, which is the number that matters when buying or packing per head.
        public int PeopleCount { get; private set; }
        public int CompetitorCount { get; private set; }
        public int SpectatorCount { get; private set; }
        public int MealsCount { get; private set; }
        public int UniquePeopleCount { get; private set; }
        public int UniqueCompetitorCount { get; private set; }
        public List<RepeatGroup> Repeats { get; private set; } = new();

        public bool HasRepeats => Repeats.Count > 0;

        // One human who appears on more than one entry.
        public class RepeatGroup
        {
            public string Who { get; init; } = "";
            // "ID number" or "name" — an ID match is all but certain, a name match is a question
            // for the organiser, because two shooters really can share a name.
            public string MatchedOn { get; init; } = "";
            public bool IsCompetitor { get; init; }
            public List<RepeatEntry> Entries { get; init; } = new();
        }

        public class RepeatEntry
        {
            public int BookingId { get; init; }
            public string Contact { get; init; } = "";
            public string Status { get; init; } = "";
            public string Detail { get; init; } = "";
        }

        // Starting-lane import state.
        public int LanesAssignedShoot1 { get; private set; }
        public int LanesAssignedShoot2 { get; private set; }
        public string? LaneImportMessage { get; private set; }
        public string? LaneImportError { get; private set; }
        // Rows from the imported sheet that couldn't be matched to exactly one person.
        public List<PendingLaneRow> PendingLanes { get; private set; } = new();
        // People eligible for each round, used to populate the manual-match dropdowns.
        public List<EventParticipant> Shoot1Candidates { get; private set; } = new();
        public List<EventParticipant> Shoot2Candidates { get; private set; } = new();

        public class PendingLaneRow
        {
            public int Shoot { get; set; }
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public int Lane { get; set; }
        }

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

            var configuredShoots = await _context.CourseTargets
                .Where(c => c.EventId == Id)
                .Select(c => c.Shoot)
                .Distinct()
                .ToListAsync();
            Shoot1Configured = configuredShoots.Contains(1);
            Shoot2Configured = configuredShoots.Contains(2);

            await LoadStatsAsync();
            await LoadRegistrationsAsync();
            await LoadLaneStateAsync();
            return Page();
        }

        // Everyone who shoots: participants on live bookings, spectators excluded. The
        // booking is included because the paid stamp on a scorecard comes from it.
        private IQueryable<EventParticipant> CompetitorsQuery() =>
            _context.EventParticipants
                .Include(p => p.EventRegistration)
                .Where(p => p.EventRegistration.EventId == Id
                         && p.EventRegistration.Status != "Cancelled"
                         && p.AttendanceType != "Spectator");

        // Loads lane assignment counts and, if an import just ran, the unmatched rows
        // (stashed in TempData) plus the candidate people for the manual match UI.
        private async Task LoadLaneStateAsync()
        {
            var competitors = await CompetitorsQuery().ToListAsync();
            LanesAssignedShoot1 = competitors.Count(p => p.StartingLaneShoot1.HasValue);
            LanesAssignedShoot2 = competitors.Count(p => p.StartingLaneShoot2.HasValue);

            LaneImportMessage = TempData["LaneImportMessage"] as string;
            LaneImportError = TempData["LaneImportError"] as string;

            if (TempData["PendingLanes"] is string json && !string.IsNullOrWhiteSpace(json))
            {
                PendingLanes = System.Text.Json.JsonSerializer.Deserialize<List<PendingLaneRow>>(json) ?? new();
            }

            if (PendingLanes.Count > 0)
            {
                Shoot1Candidates = FilterForShoot(competitors, Event, 1).OrderBy(p => p.Surname).ThenBy(p => p.Name).ToList();
                Shoot2Candidates = FilterForShoot(competitors, Event, 2).OrderBy(p => p.Surname).ThenBy(p => p.Name).ToList();
            }
        }

        // People who take part in a given round. Single-round events ignore the round
        // number; double-headers split on ShootSelection (First/Second/Both). A provincial
        // two-day event enters every competitor for both days, so nothing is filtered out.
        private static IEnumerable<EventParticipant> FilterForShoot(IEnumerable<EventParticipant> people, Event ev, int shoot)
        {
            if (!ev.IsDoubleHeader) return people;
            return shoot == 2
                ? people.Where(p => p.ShootSelection == "Second" || p.ShootSelection == "Both")
                : people.Where(p => p.ShootSelection == "First" || p.ShootSelection == "Both");
        }

        public async Task<IActionResult> OnGetSampleScorecardAsync(int shoot = 1)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var course = await LoadCourseForShootAsync(ev, shoot);

            var sample = new ScorecardPdfService.ParticipantInfo
            {
                Name = "Sample",
                Surname = "Shooter",
                Club = "POLK",
                MembershipNumber = "19-0000",
                Division = "Open",
                GunType = "PCP"
            };

            var suffix = RoundSuffix(ev, shoot);
            var pdf = _scorecardService.GenerateBatch(ev, course, new[] { sample }, shoot);
            return File(pdf, "application/pdf", $"scorecard_sample{suffix}_{ev.Id}.pdf");
        }

        public async Task<IActionResult> OnGetGenerateScorecardsAsync(int shoot = 1)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var course = await LoadCourseForShootAsync(ev, shoot);

            // Spectators don't shoot, so they're never printed on scorecards.
            var competitors = await CompetitorsQuery()
                .OrderBy(p => p.Surname).ThenBy(p => p.Name)
                .ToListAsync();

            var participants = new List<ScorecardPdfService.ParticipantInfo>();
            foreach (var person in FilterForShoot(competitors, ev, shoot))
                participants.Add(await EnrichAsync(person, shoot));

            // 6 blank cards for walk-ins
            for (int i = 0; i < 6; i++)
                participants.Add(new ScorecardPdfService.ParticipantInfo());

            var pdf = _scorecardService.GenerateBatch(ev, course, participants, shoot);
            var safeTitle = string.Concat((ev.Title ?? "event").Split(Path.GetInvalidFileNameChars()));
            return File(pdf, "application/pdf", $"scorecards_{safeTitle}{RoundSuffix(ev, shoot)}_{ev.Id}.pdf");
        }

        private static string RoundSuffix(Event ev, int shoot) =>
            ev.HasTwoRounds ? $"_{ev.RoundLabel.ToLowerInvariant()}{shoot}" : "";

        private async Task<List<CourseTarget>> LoadCourseForShootAsync(Event ev, int shoot)
        {
            // Same-course events always read Shoot=1, as do single-round events.
            var effectiveShoot = (ev.HasTwoRounds && !ev.UseSameCourseForBothShoots && shoot == 2) ? 2 : 1;
            return await _context.CourseTargets
                .Where(c => c.EventId == ev.Id && c.Shoot == effectiveShoot)
                .OrderBy(c => c.TargetNumber)
                .ToListAsync();
        }

        private async Task<ScorecardPdfService.ParticipantInfo> EnrichAsync(EventParticipant person, int shoot = 1)
        {
            var isPaid = person.EventRegistration?.Status == "Paid";
            var lane = shoot == 2 ? person.StartingLaneShoot2 : person.StartingLaneShoot1;

            // 1. Try name lookup first (only returns when exactly one match)
            var api = await _sahftaClient.LookupByNameAsync(person.Name, person.Surname);

            // 2. Fall back to SAHFTA membership number if supplied
            if (api == null &&
                !string.IsNullOrWhiteSpace(person.SAHFTANumber) &&
                !person.SAHFTANumber.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !person.SAHFTANumber.Equals("n/a", StringComparison.OrdinalIgnoreCase) &&
                person.SAHFTANumber != "0")
            {
                api = await _sahftaClient.LookupByMembershipNumberAsync(person.SAHFTANumber);
            }

            if (api != null)
            {
                return new ScorecardPdfService.ParticipantInfo
                {
                    Name = api.firstName ?? person.Name,
                    Surname = api.surname ?? person.Surname,
                    Club = api.club ?? (person.ClubName ?? ""),
                    MembershipNumber = api.membershipNumber ?? (person.SAHFTANumber ?? ""),
                    Division = api.leaderboardDivision ?? person.Division,
                    GunType = person.GunType,
                    IsPaid = isPaid,
                    StartingLane = lane
                };
            }

            // No API match → use the registration, mark club/membership N/A per your rule
            return new ScorecardPdfService.ParticipantInfo
            {
                Name = person.Name,
                Surname = person.Surname,
                Club = "N/A",
                MembershipNumber = "N/A",
                Division = person.Division,
                GunType = person.GunType,
                IsPaid = isPaid,
                StartingLane = lane
            };
        }

        public async Task<IActionResult> OnGetScoresheetAsync(int shoot = 1)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            // Spectators don't shoot, so they're never included on the scoresheet.
            var competitors = await CompetitorsQuery()
                .OrderBy(p => p.Surname).ThenBy(p => p.Name)
                .ToListAsync();

            var participants = new List<ScorecardPdfService.ParticipantInfo>();
            foreach (var person in FilterForShoot(competitors, ev, shoot))
                participants.Add(await EnrichAsync(person, shoot));

            var xlsx = _excelService.GenerateScoresheet(ev, participants, shoot);
            var safeTitle = string.Concat((ev.Title ?? "event").Split(Path.GetInvalidFileNameChars()));
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"scoresheet_{safeTitle}{RoundSuffix(ev, shoot)}_{ev.Id}.xlsx");
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

        public async Task<IActionResult> OnGetUnpaidSheetAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            // Every booking that still owes: not yet Paid, not Cancelled. "Confirmed" is used
            // for free registrations (nothing to collect), so they're excluded too. Spectators
            // pay no entry fee but can still owe for meals, so the amount decides — not the
            // attendance type.
            var bookings = await _context.EventRegistrations
                .Include(r => r.Participants)
                .Where(r => r.EventId == Id && r.Status != "Paid" && r.Status != "Cancelled")
                .OrderBy(r => r.Surname).ThenBy(r => r.Name)
                .ToListAsync();

            var rows = new List<ExcelExportService.UnpaidRow>();
            foreach (var r in bookings)
            {
                var owing = EventFeeCalculator.ForBooking(ev, r);
                if (owing <= 0) continue; // nothing outstanding to collect

                var people = r.Participants.OrderBy(p => p.Position).ToList();
                var solo = people.Count == 1 ? people[0] : null;

                rows.Add(new ExcelExportService.UnpaidRow
                {
                    Surname = r.Surname,
                    Name = r.Name,
                    Cell = r.CellNumber,
                    // Only worth listing when the booking covers more than the contact alone.
                    Party = people.Count > 1
                        ? string.Join(", ", people.Select(p => p.FullName + (p.IsSpectator ? " (spec)" : "")))
                        : "",
                    Gun = solo?.GunType ?? "",
                    Division = solo?.Division ?? "",
                    Shoot = solo?.ShootSelection switch
                    {
                        "First" => $"{ev.RoundLabel} 1",
                        "Second" => $"{ev.RoundLabel} 2",
                        "Both" => "Both",
                        _ => ""
                    },
                    Meals = r.ExtraMeals,
                    AmountOwing = owing,
                    Method = r.PaymentMethod ?? ""
                });
            }

            var xlsx = _excelService.GenerateUnpaidSheet(ev, rows);
            var safeTitle = string.Concat((ev.Title ?? "event").Split(Path.GetInvalidFileNameChars()));
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"to_collect_{safeTitle}_{ev.Id}.xlsx");
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            var bookings = await _context.EventRegistrations
                .Include(r => r.Participants)
                .Where(r => r.EventId == Id)
                .OrderBy(r => r.RegistrationDate)
                .ToListAsync();

            // One row per person, with the booking's contact and payment repeated. Bookings
            // are grouped by BookingId so a multi-person entry reads as a block.
            var sb = new StringBuilder();
            sb.AppendLine("BookingId,Date,Contact,ContactSurname,Email,Cell,ContactId,Position,Name,Surname,IdNumber,Attendance,GunType,Rifle,Division,OtherDivision,SAHFTA,Club,Shoot,Lane1,Lane2,GuardianName,GuardianSurname,IndemnityAgreed,GuardianIndemnity,SocialMediaConsent,ExtraMeals,PaymentMethod,Status,BookingTotal,AmountPaid,Reference");
            foreach (var r in bookings)
            {
                var total = EventFeeCalculator.ForBooking(ev, r);
                foreach (var p in r.Participants.OrderBy(p => p.Position))
                {
                    sb.Append(r.Id).Append(',')
                      .Append(r.RegistrationDate.ToString("yyyy-MM-dd HH:mm")).Append(',')
                      .Append(Csv(r.Name)).Append(',')
                      .Append(Csv(r.Surname)).Append(',')
                      .Append(Csv(r.EmailAddress)).Append(',')
                      .Append(Csv(r.CellNumber)).Append(',')
                      .Append(Csv(r.IdNumber)).Append(',')
                      .Append(p.Position).Append(',')
                      .Append(Csv(p.Name)).Append(',')
                      .Append(Csv(p.Surname)).Append(',')
                      .Append(Csv(p.IdNumber)).Append(',')
                      .Append(Csv(p.AttendanceType)).Append(',')
                      .Append(Csv(p.GunType)).Append(',')
                      .Append(Csv(p.RifleOwnership)).Append(',')
                      .Append(Csv(p.Division)).Append(',')
                      .Append(Csv(p.OtherDivision)).Append(',')
                      .Append(Csv(p.SAHFTANumber)).Append(',')
                      .Append(Csv(p.ClubName)).Append(',')
                      .Append(Csv(p.ShootSelection)).Append(',')
                      .Append(p.StartingLaneShoot1?.ToString() ?? "").Append(',')
                      .Append(p.StartingLaneShoot2?.ToString() ?? "").Append(',')
                      .Append(Csv(p.GuardianName)).Append(',')
                      .Append(Csv(p.GuardianSurname)).Append(',')
                      .Append(p.IndemnityAgreed).Append(',')
                      .Append(p.GuardianIndemnityAgreed).Append(',')
                      .Append(Csv(p.SocialMediaConsent)).Append(',')
                      .Append(r.ExtraMeals).Append(',')
                      .Append(Csv(r.PaymentMethod)).Append(',')
                      .Append(Csv(r.Status)).Append(',')
                      // Invariant, or the ZA locale writes "390,00" and splits the column.
                      .Append(total.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                      .Append(r.AmountPaid.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                      .Append(Csv(r.PaymentReference))
                      .AppendLine();
                }
            }

            var safeTitle = string.Concat(ev.Title.Split(Path.GetInvalidFileNameChars()));
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"registrations_{safeTitle}_{ev.Id}.csv");
        }

        public async Task<IActionResult> OnPostMarkPaidAsync(int registrationId)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var reg = await _context.EventRegistrations
                .Include(r => r.Event)
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (reg != null && reg.Status != "Paid")
            {
                reg.Status = "Paid";
                if (reg.Event != null && reg.AmountPaid == 0)
                    reg.AmountPaid = EventFeeCalculator.ForBooking(reg.Event, reg);
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

        // Imports the squadding spreadsheet and auto-assigns starting lanes wherever a
        // row matches exactly one person (by Name+Surname, or unique surname when
        // no first name is given). Ambiguous/unmatched rows go to the manual-match list.
        public async Task<IActionResult> OnPostImportLanesAsync(IFormFile? laneFile)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            var ev = await _context.Events.FindAsync(Id);
            if (ev == null) return NotFound();

            if (laneFile == null || laneFile.Length == 0)
            {
                TempData["LaneImportError"] = "Please choose a spreadsheet to import.";
                return RedirectToPage(new { id = Id });
            }

            var competitors = await CompetitorsQuery().ToListAsync();

            // Per-round candidate pools; a person can only be claimed by one row per round.
            var pools = new Dictionary<int, List<EventParticipant>>
            {
                [1] = FilterForShoot(competitors, ev, 1).ToList(),
                [2] = FilterForShoot(competitors, ev, 2).ToList()
            };
            var used = new Dictionary<int, HashSet<int>> { [1] = new(), [2] = new() };

            int assigned = 0;
            var pending = new List<PendingLaneRow>();

            try
            {
                using var stream = laneFile.OpenReadStream();
                using var wb = new ClosedXML.Excel.XLWorkbook(stream);

                // Single-round events: only the first sheet, mapped to round 1.
                var sheets = ev.HasTwoRounds
                    ? wb.Worksheets.ToList()
                    : new List<ClosedXML.Excel.IXLWorksheet> { wb.Worksheets.First() };

                foreach (var ws in sheets)
                {
                    int shoot = ShootOfSheet(ws.Name, ev.HasTwoRounds);
                    if (shoot == 0) continue; // can't tell which round — skip

                    if (!FindHeader(ws, out int headerRow, out int nameCol, out int surCol, out int laneCol))
                        continue;

                    var pool = pools[shoot];
                    var usedSet = used[shoot];

                    foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow))
                    {
                        var name = nameCol > 0 ? row.Cell(nameCol).GetString().Trim() : "";
                        var surname = row.Cell(surCol).GetString().Trim();
                        var laneStr = row.Cell(laneCol).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(surname) && string.IsNullOrWhiteSpace(name)) continue;
                        if (!TryParseLane(laneStr, out int lane)) continue;

                        var matches = pool.Where(p =>
                            !usedSet.Contains(p.Id) &&
                            Norm(p.Surname) == Norm(surname) &&
                            (name.Length == 0 || Norm(p.Name) == Norm(name))).ToList();

                        if (matches.Count == 1)
                        {
                            var person = matches[0];
                            if (shoot == 2) person.StartingLaneShoot2 = lane; else person.StartingLaneShoot1 = lane;
                            usedSet.Add(person.Id);
                            assigned++;
                        }
                        else
                        {
                            pending.Add(new PendingLaneRow { Shoot = shoot, Name = name, Surname = surname, Lane = lane });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["LaneImportError"] = $"Could not read the spreadsheet: {ex.Message}";
                return RedirectToPage(new { id = Id });
            }

            await _context.SaveChangesAsync();

            TempData["LaneImportMessage"] = pending.Count == 0
                ? $"Imported starting lanes: {assigned} assigned automatically."
                : $"Imported starting lanes: {assigned} assigned automatically, {pending.Count} need a manual match below.";
            if (pending.Count > 0)
                TempData["PendingLanes"] = System.Text.Json.JsonSerializer.Serialize(pending);

            return RedirectToPage(new { id = Id });
        }

        // Saves the manual matches: each posted row carries its round, lane, and the chosen
        // participant id (0 = skip). Arrays are aligned positionally by form order.
        public async Task<IActionResult> OnPostAssignLanesAsync(int[] shoots, int[] lanes, int[] regIds)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            int count = Math.Min(regIds.Length, Math.Min(shoots.Length, lanes.Length));
            int assigned = 0;
            for (int i = 0; i < count; i++)
            {
                if (regIds[i] <= 0) continue;
                var person = await _context.EventParticipants
                    .FirstOrDefaultAsync(p => p.Id == regIds[i] && p.EventRegistration.EventId == Id);
                if (person == null) continue;
                if (shoots[i] == 2) person.StartingLaneShoot2 = lanes[i]; else person.StartingLaneShoot1 = lanes[i];
                assigned++;
            }
            await _context.SaveChangesAsync();

            TempData["LaneImportMessage"] = $"Saved {assigned} manual lane assignment(s).";
            return RedirectToPage(new { id = Id });
        }

        // Decides which round a worksheet belongs to from its name — "Shoot 2", "Day 2" and
        // "Dag 2" all land on round 2. Single-round events always map to round 1. Returns 0
        // when a two-round sheet name is ambiguous.
        private static int ShootOfSheet(string sheetName, bool hasTwoRounds)
        {
            if (!hasTwoRounds) return 1;
            var n = (sheetName ?? "").ToLowerInvariant().Replace(" ", "");
            if (n.Contains("2")) return 2;
            if (n.Contains("1")) return 1;
            return 0;
        }

        // Finds the header row and the Name/Surname/Lane column numbers. Name is optional
        // (some sheets list surnames only); Surname and Lane are required.
        private static bool FindHeader(ClosedXML.Excel.IXLWorksheet ws, out int headerRow, out int nameCol, out int surCol, out int laneCol)
        {
            headerRow = 0; nameCol = 0; surCol = 0; laneCol = 0;
            foreach (var row in ws.RowsUsed().Take(10))
            {
                int n = 0, s = 0, l = 0;
                foreach (var cell in row.CellsUsed())
                {
                    var txt = cell.GetString().Trim().ToLowerInvariant();
                    if (txt is "name" or "first name" or "firstname" or "naam") n = cell.Address.ColumnNumber;
                    else if (txt is "surname" or "last name" or "lastname" or "van") s = cell.Address.ColumnNumber;
                    else if (txt.Contains("lane") || txt.Contains("baan")) l = cell.Address.ColumnNumber;
                }
                if (s > 0 && l > 0)
                {
                    headerRow = row.RowNumber(); nameCol = n; surCol = s; laneCol = l;
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseLane(string raw, out int lane)
        {
            lane = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            // Accept "5", "5.0", "Lane 5" etc.
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out lane) && lane > 0;
        }

        private static string Norm(string? s) =>
            System.Text.RegularExpressions.Regex.Replace((s ?? "").Trim().ToLowerInvariant(), "\\s+", " ");

        private async Task LoadStatsAsync()
        {
            var all = await _context.EventRegistrations
                .Include(r => r.Participants)
                .Where(r => r.EventId == Id)
                .ToListAsync();

            CancelledCount = all.Count(r => r.Status == "Cancelled");

            var live = all.Where(r => r.Status != "Cancelled").ToList();
            BookingCount = live.Count;
            PaidCount = live.Count(r => r.Status == "Paid");
            PendingCount = live.Count(r => r.Status == "Pending");
            ConfirmedCount = live.Count(r => r.Status == "Confirmed");
            OtherStatusCount = BookingCount - PaidCount - PendingCount - ConfirmedCount;

            var people = live.SelectMany(r => r.Participants).ToList();
            PeopleCount = people.Count;
            CompetitorCount = people.Count(p => !p.IsSpectator);
            SpectatorCount = people.Count(p => p.IsSpectator);
            MealsCount = live.Sum(r => r.ExtraMeals);

            Repeats = FindRepeats(live);
            UniquePeopleCount = PeopleCount - Repeats.Sum(g => g.Entries.Count - 1);
            UniqueCompetitorCount = CompetitorCount
                - Repeats.Where(g => g.IsCompetitor).Sum(g => g.Entries.Count - 1);
        }

        // Entries that look like the same person registering more than once — across separate
        // bookings or twice on one. Matching is by ID number first, because that is decisive;
        // whoever is left is matched on name, which is a hint rather than a verdict. Nothing is
        // subtracted silently: the page lists each group so the organiser settles it, since
        // guessing low here means a competitor arrives to no goodie bag.
        private static List<RepeatGroup> FindRepeats(IEnumerable<EventRegistration> live)
        {
            var rows = live
                .SelectMany(r => r.Participants.Select(p => (Reg: r, P: p)))
                .ToList();

            var groups = new List<RepeatGroup>();
            var grouped = new HashSet<int>();

            void Collect(IEnumerable<IGrouping<string, (EventRegistration Reg, EventParticipant P)>> buckets, string matchedOn)
            {
                foreach (var bucket in buckets.Where(b => b.Count() > 1))
                {
                    var members = bucket.ToList();
                    groups.Add(new RepeatGroup
                    {
                        Who = members[0].P.FullName,
                        MatchedOn = matchedOn,
                        IsCompetitor = members.Any(m => !m.P.IsSpectator),
                        Entries = members.Select(m => new RepeatEntry
                        {
                            BookingId = m.Reg.Id,
                            Contact = $"{m.Reg.Name} {m.Reg.Surname}".Trim(),
                            Status = m.Reg.Status ?? "",
                            Detail = Describe(m.P)
                        }).ToList()
                    });

                    foreach (var m in members) grouped.Add(m.P.Id);
                }
            }

            Collect(rows.Where(x => !string.IsNullOrWhiteSpace(x.P.IdNumber))
                        .GroupBy(x => NormId(x.P.IdNumber)),
                    "ID number");

            Collect(rows.Where(x => !grouped.Contains(x.P.Id))
                        .Where(x => !string.IsNullOrWhiteSpace(x.P.FullName))
                        .GroupBy(x => Norm(x.P.FullName)),
                    "name");

            return groups.OrderBy(g => g.Who).ToList();
        }

        // Enough detail next to a repeat to tell two same-named shooters apart at a glance.
        private static string Describe(EventParticipant p)
        {
            var bits = new List<string>();
            if (!string.IsNullOrWhiteSpace(p.IdNumber)) bits.Add($"ID {p.IdNumber}");
            if (!string.IsNullOrWhiteSpace(p.SAHFTANumber)) bits.Add($"SAHFTA {p.SAHFTANumber}");
            if (!string.IsNullOrWhiteSpace(p.ClubName)) bits.Add(p.ClubName!);
            if (!string.IsNullOrWhiteSpace(p.Division)) bits.Add(p.Division!);
            if (p.IsSpectator) bits.Add("spectator");
            return string.Join(" · ", bits);
        }

        private static string NormId(string? s) =>
            System.Text.RegularExpressions.Regex.Replace((s ?? "").Trim().ToUpperInvariant(), "[^A-Z0-9]", "");

        private async Task LoadRegistrationsAsync()
        {
            var q = _context.EventRegistrations
                .Include(r => r.Participants)
                .Where(r => r.EventId == Id);

            // Search covers the booking contact and anyone on the booking, so looking up a
            // child by name finds the parent's booking.
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(r =>
                    r.Name.Contains(s) ||
                    r.Surname.Contains(s) ||
                    r.EmailAddress.Contains(s) ||
                    r.CellNumber.Contains(s) ||
                    r.Participants.Any(p => p.Name.Contains(s) || p.Surname.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
                q = q.Where(r => r.Status == StatusFilter);

            Registrations = await q.OrderByDescending(r => r.RegistrationDate).ToListAsync();
        }

        // The amount a booking owes in total, for display on the list.
        public decimal BookingTotal(EventRegistration reg) => EventFeeCalculator.ForBooking(Event, reg);

        private static string Csv(string? v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            var needsQuote = v.Contains(',') || v.Contains('"') || v.Contains('\n');
            var escaped = v.Replace("\"", "\"\"");
            return needsQuote ? $"\"{escaped}\"" : escaped;
        }
    }
}

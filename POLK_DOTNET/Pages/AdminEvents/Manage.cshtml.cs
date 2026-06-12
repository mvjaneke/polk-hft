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
        public List<EventRegistration> Registrations { get; private set; } = new();

        // Whether a Troyer course actually has target rows for each shoot. Drives the
        // scorecard/score-sheet buttons. Based on real CourseTargets rows rather than
        // Event.CourseTargetCount, which is a Shoot-1-only flag that an event edit can wipe.
        public bool Shoot1Configured { get; private set; }
        public bool Shoot2Configured { get; private set; }

        // Shoot 2 reuses the Shoot-1 course when "use same course for both shoots" is on.
        public bool Shoot1Ready => Shoot1Configured;
        public bool Shoot2Ready => Event.UseSameCourseForBothShoots ? Shoot1Configured : Shoot2Configured;
        public bool CourseConfigured => Shoot1Ready || Shoot2Ready;

        public int TotalCount { get; private set; }
        public int PaidCount { get; private set; }
        public int PendingCount { get; private set; }
        public int ConfirmedCount { get; private set; }
        public int CancelledCount { get; private set; }

        // Starting-lane import state.
        public int LanesAssignedShoot1 { get; private set; }
        public int LanesAssignedShoot2 { get; private set; }
        public string? LaneImportMessage { get; private set; }
        public string? LaneImportError { get; private set; }
        // Rows from the imported sheet that couldn't be matched to exactly one registration.
        public List<PendingLaneRow> PendingLanes { get; private set; } = new();
        // Registrations eligible for each shoot, used to populate the manual-match dropdowns.
        public List<EventRegistration> Shoot1Candidates { get; private set; } = new();
        public List<EventRegistration> Shoot2Candidates { get; private set; } = new();

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

        // Loads lane assignment counts and, if an import just ran, the unmatched rows
        // (stashed in TempData) plus the candidate registrations for the manual match UI.
        private async Task LoadLaneStateAsync()
        {
            var all = await _context.EventRegistrations.Where(r => r.EventId == Id).ToListAsync();
            LanesAssignedShoot1 = all.Count(r => r.StartingLaneShoot1.HasValue);
            LanesAssignedShoot2 = all.Count(r => r.StartingLaneShoot2.HasValue);

            LaneImportMessage = TempData["LaneImportMessage"] as string;
            LaneImportError = TempData["LaneImportError"] as string;

            if (TempData["PendingLanes"] is string json && !string.IsNullOrWhiteSpace(json))
            {
                PendingLanes = System.Text.Json.JsonSerializer.Deserialize<List<PendingLaneRow>>(json) ?? new();
            }

            if (PendingLanes.Count > 0)
            {
                var active = all.Where(r => r.Status != "Cancelled" && r.AttendanceType != "Spectator").ToList();
                Shoot1Candidates = FilterForShoot(active, Event, 1).OrderBy(r => r.Surname).ThenBy(r => r.Name).ToList();
                Shoot2Candidates = FilterForShoot(active, Event, 2).OrderBy(r => r.Surname).ThenBy(r => r.Name).ToList();
            }
        }

        // Registrations that take part in a given shoot. Single-shoot events ignore the
        // shoot number; double-headers split on ShootSelection (First/Second/Both).
        private static IEnumerable<EventRegistration> FilterForShoot(IEnumerable<EventRegistration> regs, Event ev, int shoot)
        {
            if (!ev.IsDoubleHeader) return regs;
            return shoot == 2
                ? regs.Where(r => r.ShootSelection == "Second" || r.ShootSelection == "Both")
                : regs.Where(r => r.ShootSelection == "First" || r.ShootSelection == "Both");
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

            var suffix = ev.IsDoubleHeader ? $"_shoot{shoot}" : "";
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
            var regsQuery = _context.EventRegistrations
                .Where(r => r.EventId == Id && r.Status != "Cancelled" && r.AttendanceType != "Spectator");

            if (ev.IsDoubleHeader)
            {
                // Shoot 1: First + Both. Shoot 2: Second + Both.
                regsQuery = shoot == 2
                    ? regsQuery.Where(r => r.ShootSelection == "Second" || r.ShootSelection == "Both")
                    : regsQuery.Where(r => r.ShootSelection == "First" || r.ShootSelection == "Both");
            }

            var regs = await regsQuery
                .OrderBy(r => r.Surname).ThenBy(r => r.Name)
                .ToListAsync();

            var participants = new List<ScorecardPdfService.ParticipantInfo>();
            foreach (var reg in regs)
            {
                participants.Add(await EnrichAsync(reg, shoot));
            }

            // 6 blank cards for walk-ins
            for (int i = 0; i < 6; i++)
                participants.Add(new ScorecardPdfService.ParticipantInfo());

            var pdf = _scorecardService.GenerateBatch(ev, course, participants, shoot);
            var safeTitle = string.Concat((ev.Title ?? "event").Split(Path.GetInvalidFileNameChars()));
            var suffix = ev.IsDoubleHeader ? $"_shoot{shoot}" : "";
            return File(pdf, "application/pdf", $"scorecards_{safeTitle}{suffix}_{ev.Id}.pdf");
        }

        private async Task<List<CourseTarget>> LoadCourseForShootAsync(Event ev, int shoot)
        {
            // Same-course double headers always read Shoot=1. Non-double-header events also use Shoot=1.
            var effectiveShoot = (ev.IsDoubleHeader && !ev.UseSameCourseForBothShoots && shoot == 2) ? 2 : 1;
            return await _context.CourseTargets
                .Where(c => c.EventId == ev.Id && c.Shoot == effectiveShoot)
                .OrderBy(c => c.TargetNumber)
                .ToListAsync();
        }

        private async Task<ScorecardPdfService.ParticipantInfo> EnrichAsync(EventRegistration reg, int shoot = 1)
        {
            var isPaid = reg.Status == "Paid";
            var lane = shoot == 2 ? reg.StartingLaneShoot2 : reg.StartingLaneShoot1;

            // 1. Try name lookup first (only returns when exactly one match)
            var api = await _sahftaClient.LookupByNameAsync(reg.Name, reg.Surname);

            // 2. Fall back to SAHFTA membership number if supplied
            if (api == null &&
                !string.IsNullOrWhiteSpace(reg.SAHFTANumber) &&
                !reg.SAHFTANumber.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !reg.SAHFTANumber.Equals("n/a", StringComparison.OrdinalIgnoreCase) &&
                reg.SAHFTANumber != "0")
            {
                api = await _sahftaClient.LookupByMembershipNumberAsync(reg.SAHFTANumber);
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
                    GunType = reg.GunType,
                    IsPaid = isPaid,
                    StartingLane = lane
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
                GunType = reg.GunType,
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
            var regsQuery = _context.EventRegistrations
                .Where(r => r.EventId == Id && r.Status != "Cancelled" && r.AttendanceType != "Spectator");

            if (ev.IsDoubleHeader)
            {
                regsQuery = shoot == 2
                    ? regsQuery.Where(r => r.ShootSelection == "Second" || r.ShootSelection == "Both")
                    : regsQuery.Where(r => r.ShootSelection == "First" || r.ShootSelection == "Both");
            }

            var regs = await regsQuery
                .OrderBy(r => r.Surname).ThenBy(r => r.Name)
                .ToListAsync();

            var participants = new List<ScorecardPdfService.ParticipantInfo>();
            foreach (var reg in regs)
                participants.Add(await EnrichAsync(reg, shoot));

            var xlsx = _excelService.GenerateScoresheet(ev, participants, shoot);
            var safeTitle = string.Concat((ev.Title ?? "event").Split(Path.GetInvalidFileNameChars()));
            var suffix = ev.IsDoubleHeader ? $"_shoot{shoot}" : "";
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"scoresheet_{safeTitle}{suffix}_{ev.Id}.xlsx");
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

            // Everyone who still owes: not yet Paid, not Cancelled. "Confirmed" is used
            // for free registrations (nothing to collect), so they're excluded too.
            // Spectators never pay an entry fee, so they don't belong on the collection sheet.
            var regs = await _context.EventRegistrations
                .Where(r => r.EventId == Id && r.Status != "Paid" && r.Status != "Cancelled" && r.AttendanceType != "Spectator")
                .OrderBy(r => r.Surname).ThenBy(r => r.Name)
                .ToListAsync();

            var rows = new List<ExcelExportService.UnpaidRow>();
            foreach (var r in regs)
            {
                var owing = Pages.RegisterEventModel.ComputeEffectiveFee(ev, r.ShootSelection, r.RifleOwnership);
                if (owing <= 0) continue; // nothing outstanding to collect

                rows.Add(new ExcelExportService.UnpaidRow
                {
                    Surname = r.Surname,
                    Name = r.Name,
                    Cell = r.CellNumber,
                    Gun = r.GunType,
                    Division = r.Division ?? "",
                    Shoot = r.ShootSelection switch
                    {
                        "First" => "Shoot 1",
                        "Second" => "Shoot 2",
                        "Both" => "Both",
                        _ => ""
                    },
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
                if (reg.Event != null && reg.AmountPaid == 0)
                    reg.AmountPaid = Pages.RegisterEventModel.ComputeEffectiveFee(reg.Event, reg.ShootSelection, reg.RifleOwnership);
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
        // row matches exactly one registration (by Name+Surname, or unique surname when
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

            var regs = await _context.EventRegistrations
                .Where(r => r.EventId == Id && r.Status != "Cancelled" && r.AttendanceType != "Spectator")
                .ToListAsync();

            // Per-shoot candidate pools; a registration can only be claimed by one row per shoot.
            var pools = new Dictionary<int, List<EventRegistration>>
            {
                [1] = FilterForShoot(regs, ev, 1).ToList(),
                [2] = FilterForShoot(regs, ev, 2).ToList()
            };
            var used = new Dictionary<int, HashSet<int>> { [1] = new(), [2] = new() };

            int assigned = 0;
            var pending = new List<PendingLaneRow>();

            try
            {
                using var stream = laneFile.OpenReadStream();
                using var wb = new ClosedXML.Excel.XLWorkbook(stream);

                // Single-shoot events: only the first sheet, mapped to shoot 1.
                var sheets = ev.IsDoubleHeader
                    ? wb.Worksheets.ToList()
                    : new List<ClosedXML.Excel.IXLWorksheet> { wb.Worksheets.First() };

                foreach (var ws in sheets)
                {
                    int shoot = ShootOfSheet(ws.Name, ev.IsDoubleHeader);
                    if (shoot == 0) continue; // can't tell which shoot — skip

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

                        var matches = pool.Where(r =>
                            !usedSet.Contains(r.Id) &&
                            Norm(r.Surname) == Norm(surname) &&
                            (name.Length == 0 || Norm(r.Name) == Norm(name))).ToList();

                        if (matches.Count == 1)
                        {
                            var reg = matches[0];
                            if (shoot == 2) reg.StartingLaneShoot2 = lane; else reg.StartingLaneShoot1 = lane;
                            usedSet.Add(reg.Id);
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

        // Saves the manual matches: each posted row carries its shoot, lane, and the chosen
        // registration id (0 = skip). Arrays are aligned positionally by form order.
        public async Task<IActionResult> OnPostAssignLanesAsync(int[] shoots, int[] lanes, int[] regIds)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage("/Admin");

            int count = Math.Min(regIds.Length, Math.Min(shoots.Length, lanes.Length));
            int assigned = 0;
            for (int i = 0; i < count; i++)
            {
                if (regIds[i] <= 0) continue;
                var reg = await _context.EventRegistrations
                    .FirstOrDefaultAsync(r => r.Id == regIds[i] && r.EventId == Id);
                if (reg == null) continue;
                if (shoots[i] == 2) reg.StartingLaneShoot2 = lanes[i]; else reg.StartingLaneShoot1 = lanes[i];
                assigned++;
            }
            await _context.SaveChangesAsync();

            TempData["LaneImportMessage"] = $"Saved {assigned} manual lane assignment(s).";
            return RedirectToPage(new { id = Id });
        }

        // Decides which shoot a worksheet belongs to from its name. Single-shoot events
        // always map to shoot 1. Returns 0 when a double-header sheet name is ambiguous.
        private static int ShootOfSheet(string sheetName, bool isDouble)
        {
            if (!isDouble) return 1;
            var n = (sheetName ?? "").ToLowerInvariant().Replace(" ", "");
            if (n.Contains("2")) return 2;
            if (n.Contains("1")) return 1;
            return 0;
        }

        // Finds the header row and the Name/Surname/Lane column numbers. Name is optional
        // (the Shoot 1 sheet lists surnames only); Surname and Lane are required.
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

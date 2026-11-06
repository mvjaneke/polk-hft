using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminMembership
{
    public class YearsModel : MembershipAdminPage
    {
        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;

        public YearsModel(ApplicationDbContext context, MembershipService membership)
        {
            _context = context;
            _membership = membership;
        }

        /// <summary>Year being edited; 0 = adding a new one; null = no form open.</summary>
        [BindProperty(SupportsGet = true)]
        public int? Edit { get; set; }

        [BindProperty]
        public YearInput Input { get; set; } = new();

        [BindProperty]
        public PaymentDetails Bank { get; set; } = new();

        public List<YearRow> Rows { get; private set; } = new();
        public ShootingYear? ApplicationsYear { get; private set; }
        public ShootingYear? CurrentYear { get; private set; }

        public class YearRow
        {
            public ShootingYear Year { get; set; } = null!;
            public int Applications { get; set; }
            public int PaidMembers { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
            if (Edit is int id && id > 0)
            {
                var y = Rows.FirstOrDefault(r => r.Year.Id == id)?.Year;
                if (y != null) Input = YearInput.From(y);
            }
            else if (Edit == 0)
            {
                // Start from the latest year, one year on, with the same fees.
                var last = Rows.Select(r => r.Year).OrderByDescending(y => y.StartDate).FirstOrDefault();
                Input = last == null
                    ? new YearInput { Name = $"{ClubClock.Today.Year}/{ClubClock.Today.Year + 1}", StartDate = new DateTime(ClubClock.Today.Year, 11, 1).ToString("yyyy-MM-dd"), EndDate = new DateTime(ClubClock.Today.Year + 1, 10, 31).ToString("yyyy-MM-dd"), RenewalReminderDaysBefore = 10 }
                    : YearInput.From(last, next: true);
            }
        }

        public async Task<IActionResult> OnPostSaveYearAsync()
        {
            await LoadAsync();
            var existing = Edit is int id && id > 0 ? Rows.FirstOrDefault(r => r.Year.Id == id)?.Year : null;
            if (Edit > 0 && existing == null) return NotFound();

            var fees = new Dictionary<string, decimal>();
            void Fee(string field, string? text, string label)
            {
                if (TryParseMoney(text, out var v)) fees[field] = v;
                else ModelState.AddModelError($"Input.{field}", $"Enter the {label} as an amount, e.g. 450.00.");
            }

            if (string.IsNullOrWhiteSpace(Input.Name)) ModelState.AddModelError("Input.Name", "Enter a name, e.g. 2027/2028.");
            var startOk = TryParseDate(Input.StartDate, out var start);
            var endOk = TryParseDate(Input.EndDate, out var end);
            if (!startOk) ModelState.AddModelError("Input.StartDate", "Enter the start date.");
            if (!endOk) ModelState.AddModelError("Input.EndDate", "Enter the end date.");
            if (startOk && endOk && end <= start) ModelState.AddModelError("Input.EndDate", "The end date must be after the start date.");
            Fee(nameof(YearInput.IndividualFee), Input.IndividualFee, "individual fee");
            Fee(nameof(YearInput.FamilyFee), Input.FamilyFee, "family fee");
            Fee(nameof(YearInput.PensionerFee), Input.PensionerFee, "pensioner fee");
            Fee(nameof(YearInput.NewMemberAdminFee), Input.NewMemberAdminFee, "admin fee");
            Fee(nameof(YearInput.SahftaAdultFee), Input.SahftaAdultFee, "SAHFTA adult fee");
            Fee(nameof(YearInput.SahftaJuniorFee), Input.SahftaJuniorFee, "SAHFTA junior fee");
            if (Input.RenewalReminderDaysBefore is < 1 or > 90)
                ModelState.AddModelError("Input.RenewalReminderDaysBefore", "Enter a number of days between 1 and 90.");

            if (startOk && endOk)
            {
                var clash = Rows.Select(r => r.Year).FirstOrDefault(y => y.Id != (existing?.Id ?? 0) && start <= y.EndDate && end >= y.StartDate);
                if (clash != null)
                    ModelState.AddModelError("Input.StartDate", $"These dates overlap {clash.Name} ({clash.StartDate:d MMM yyyy} to {clash.EndDate:d MMM yyyy}).");
            }

            if (!ModelState.IsValid) return Page();

            var y = existing ?? new ShootingYear { CreatedAt = DateTime.UtcNow };
            y.Name = Input.Name!.Trim();
            y.StartDate = start;
            y.EndDate = end;
            y.IndividualFee = fees[nameof(YearInput.IndividualFee)];
            y.FamilyFee = fees[nameof(YearInput.FamilyFee)];
            y.PensionerFee = fees[nameof(YearInput.PensionerFee)];
            y.NewMemberAdminFee = fees[nameof(YearInput.NewMemberAdminFee)];
            y.SahftaAdultFee = fees[nameof(YearInput.SahftaAdultFee)];
            y.SahftaJuniorFee = fees[nameof(YearInput.SahftaJuniorFee)];
            y.RenewalReminderDaysBefore = Input.RenewalReminderDaysBefore;
            if (existing == null) _context.ShootingYears.Add(y);
            await _context.SaveChangesAsync();

            var before = Rows.Select(r => r.Year).Where(p => p.EndDate < y.StartDate).OrderByDescending(p => p.EndDate).FirstOrDefault();
            var opens = before != null && before.EndDate.AddDays(1) >= y.StartDate.AddDays(-7) ? before.RenewalOpensOn : y.StartDate;
            Message = existing == null
                ? $"{y.Name} added. The Apply page starts selling it on {opens:d MMMM yyyy}."
                : $"{y.Name} saved. Applications already submitted keep the fees they were charged.";
            return RedirectToPage(new { Edit = (int?)null });
        }

        public async Task<IActionResult> OnPostDeleteYearAsync(int id)
        {
            await LoadAsync();
            var row = Rows.FirstOrDefault(r => r.Year.Id == id);
            if (row == null) return NotFound();
            var reminders = await _context.MembershipReminderLogs.AnyAsync(r => r.ShootingYearId == id);
            if (row.Applications > 0 || row.PaidMembers > 0 || reminders)
            {
                ErrorMessage = $"{row.Year.Name} can't be deleted because applications or paid members are recorded against it.";
                return RedirectToPage();
            }
            _context.ShootingYears.Remove(row.Year);
            await _context.SaveChangesAsync();
            Message = $"{row.Year.Name} deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSaveBankAsync()
        {
            await _membership.SetSettingAsync(MembershipService.SettingKeys.AccountName, Bank.AccountName?.Trim() ?? "");
            await _membership.SetSettingAsync(MembershipService.SettingKeys.BankName, Bank.BankName?.Trim() ?? "");
            await _membership.SetSettingAsync(MembershipService.SettingKeys.AccountNumber, Bank.AccountNumber?.Trim() ?? "");
            await _membership.SetSettingAsync(MembershipService.SettingKeys.BranchCode, Bank.BranchCode?.Trim() ?? "");
            await _membership.SetSettingAsync(MembershipService.SettingKeys.ProofOfPaymentEmail, Bank.ProofOfPaymentEmail?.Trim() ?? "");
            await _context.SaveChangesAsync();
            Message = "Payment details saved.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var years = await _context.ShootingYears.ToListAsync();
            var appCounts = await _context.MembershipApplications.Where(a => a.ShootingYearId != null)
                .GroupBy(a => a.ShootingYearId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var memberCounts = await _context.MemberYears
                .GroupBy(y => y.ShootingYearId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();

            Rows = years.OrderByDescending(y => y.StartDate).Select(y => new YearRow
            {
                Year = y,
                Applications = appCounts.FirstOrDefault(c => c.Key == y.Id)?.N ?? 0,
                PaidMembers = memberCounts.FirstOrDefault(c => c.Key == y.Id)?.N ?? 0
            }).ToList();

            ApplicationsYear = await _membership.GetYearForApplicationsAsync();
            CurrentYear = await _membership.GetCurrentYearAsync();
            Bank = await _membership.GetPaymentDetailsAsync();
        }

        public class YearInput
        {
            public string? Name { get; set; }
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public string? IndividualFee { get; set; }
            public string? FamilyFee { get; set; }
            public string? PensionerFee { get; set; }
            public string? NewMemberAdminFee { get; set; }
            public string? SahftaAdultFee { get; set; }
            public string? SahftaJuniorFee { get; set; }
            public int RenewalReminderDaysBefore { get; set; } = 10;

            public static YearInput From(ShootingYear y, bool next = false)
            {
                var start = next ? y.EndDate.AddDays(1) : y.StartDate;
                var end = next ? y.EndDate.AddYears(1) : y.EndDate;
                return new YearInput
                {
                    Name = next ? $"{start.Year}/{end.Year}" : y.Name,
                    StartDate = start.ToString("yyyy-MM-dd"),
                    EndDate = end.ToString("yyyy-MM-dd"),
                    IndividualFee = MoneyInput(y.IndividualFee),
                    FamilyFee = MoneyInput(y.FamilyFee),
                    PensionerFee = MoneyInput(y.PensionerFee),
                    NewMemberAdminFee = MoneyInput(y.NewMemberAdminFee),
                    SahftaAdultFee = MoneyInput(y.SahftaAdultFee),
                    SahftaJuniorFee = MoneyInput(y.SahftaJuniorFee),
                    RenewalReminderDaysBefore = y.RenewalReminderDaysBefore
                };
            }
        }
    }
}

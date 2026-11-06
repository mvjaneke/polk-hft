using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminMembership
{
    public class MembersModel : MembershipAdminPage
    {
        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;

        public MembersModel(ApplicationDbContext context, MembershipService membership)
        {
            _context = context;
            _membership = membership;
        }

        [BindProperty(SupportsGet = true)]
        public int? YearId { get; set; }

        /// <summary>paid, notrenewed, unpaid (never paid), all, archived</summary>
        [BindProperty(SupportsGet = true)]
        public string Show { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        public List<ShootingYear> Years { get; private set; } = new();
        public ShootingYear? Year { get; private set; }
        public ShootingYear? PreviousYear { get; private set; }
        public List<ClubMember> Members { get; private set; } = new();

        public int OnList { get; private set; }
        public int PaidCount { get; private set; }
        public int NotRenewedCount { get; private set; }
        public int NeverPaidCount { get; private set; }
        public int ArchivedCount { get; private set; }

        public async Task OnGetAsync() => await LoadAsync();

        public async Task<IActionResult> OnGetCsvAsync()
        {
            await LoadAsync();
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[] { "Surname", "First name", "ID number", "Gender", "Contact", "Email", "SAHFTA no.", "Under 18",
                "Street", "Complex", "Suburb", "City", "Province", "Postal code", "Years paid", $"Paid {Year?.Name}" }.Select(Csv)));
            foreach (var m in Members)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    m.Surname, m.FirstName, m.IdNumber, m.Gender, m.ContactNumber, m.EmailAddress, m.SahftaNumber, m.IsJunior ? "Yes" : "No",
                    m.StreetAddress, m.ComplexOrBuilding, m.Suburb, m.City, m.Province, m.PostalCode,
                    string.Join(" ", m.Years.OrderBy(y => y.ShootingYear.StartDate).Select(y => y.ShootingYear.Name)),
                    Year != null && m.Years.Any(y => y.ShootingYearId == Year.Id) ? "Yes" : "No"
                }.Select(Csv)));
            }
            var name = $"POLK-members-{Show}-{(Year?.Name ?? "all").Replace('/', '-')}.csv";
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", name);
        }

        private static string Csv(string? v)
        {
            v ??= "";
            // Names come from a public form; stop a spreadsheet treating "=..." as a formula.
            if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        }

        private async Task LoadAsync()
        {
            Years = await _context.ShootingYears.OrderByDescending(y => y.StartDate).ToListAsync();
            if (YearId == null)
                YearId = (await _membership.GetCurrentYearAsync())?.Id ?? Years.FirstOrDefault()?.Id;
            Year = Years.FirstOrDefault(y => y.Id == YearId);
            PreviousYear = Year == null ? null : Years.Where(y => y.EndDate < Year.StartDate).OrderByDescending(y => y.StartDate).FirstOrDefault();

            var all = await _context.ClubMembers
                .Include(m => m.Years).ThenInclude(y => y.ShootingYear)
                .ToListAsync();

            var yearId = Year?.Id ?? -1;
            var prevId = PreviousYear?.Id ?? -2;
            bool PaidThis(ClubMember m) => m.Years.Any(y => y.ShootingYearId == yearId);
            bool NotRenewed(ClubMember m) => !PaidThis(m) && m.Years.Any(y => y.ShootingYearId == prevId);

            var active = all.Where(m => !m.IsArchived).ToList();
            OnList = active.Count;
            PaidCount = active.Count(PaidThis);
            NotRenewedCount = active.Count(NotRenewed);
            NeverPaidCount = active.Count(m => m.Years.Count == 0);
            ArchivedCount = all.Count - active.Count;

            IEnumerable<ClubMember> list = Show switch
            {
                "paid" => active.Where(PaidThis),
                "notrenewed" => active.Where(NotRenewed),
                "unpaid" => active.Where(m => m.Years.Count == 0),
                "archived" => all.Where(m => m.IsArchived),
                _ => active
            };

            if (!string.IsNullOrWhiteSpace(Q))
            {
                var q = Q.Trim();
                var qId = ClubMember.NormaliseIdNumber(q);
                list = list.Where(m =>
                    m.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    $"{m.Surname} {m.FirstName}".Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (m.EmailAddress ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (m.SahftaNumber ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (qId.Length >= 3 && m.IdNumber.Contains(qId)));
            }

            Members = list.OrderBy(m => m.Surname, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.FirstName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

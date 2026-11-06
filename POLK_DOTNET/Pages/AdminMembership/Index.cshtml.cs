using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminMembership
{
    public class IndexModel : MembershipAdminPage
    {
        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;

        public IndexModel(ApplicationDbContext context, MembershipService membership)
        {
            _context = context;
            _membership = membership;
        }

        /// <summary>Shooting year id; 0 = all years.</summary>
        [BindProperty(SupportsGet = true)]
        public int? YearId { get; set; }

        /// <summary>all, attention, unpaid, paid, flagged, rejected</summary>
        [BindProperty(SupportsGet = true)]
        public string Show { get; set; } = "all";

        public List<ShootingYear> Years { get; private set; } = new();
        public List<MembershipApplication> Applications { get; private set; } = new();
        public Dictionary<string, int> Counts { get; private set; } = new();
        public ShootingYear? SelectedYear { get; private set; }

        public async Task OnGetAsync()
        {
            Years = await _context.ShootingYears.OrderByDescending(y => y.StartDate).ToListAsync();

            if (YearId == null)
                YearId = (await _membership.GetYearForApplicationsAsync())?.Id ?? 0;
            SelectedYear = Years.FirstOrDefault(y => y.Id == YearId);

            var query = _context.MembershipApplications
                .Include(a => a.Members)
                .Include(a => a.ShootingYear)
                .AsQueryable();
            if (YearId > 0) query = query.Where(a => a.ShootingYearId == YearId);
            else if (YearId == -1) query = query.Where(a => a.ShootingYearId == null);

            var all = await query.ToListAsync();
            all = all.OrderByDescending(a => a.SubmittedDate).ToList();

            bool Attention(MembershipApplication a) =>
                a.ApprovalStatus != ApprovalStatuses.Rejected &&
                (!a.IsPaid || a.HasOpenFlags || a.ApprovalStatus == ApprovalStatuses.Pending);

            Counts = new Dictionary<string, int>
            {
                ["all"] = all.Count,
                ["attention"] = all.Count(Attention),
                ["unpaid"] = all.Count(a => !a.IsPaid && a.ApprovalStatus != ApprovalStatuses.Rejected),
                ["paid"] = all.Count(a => a.IsPaid),
                ["flagged"] = all.Count(a => a.HasOpenFlags),
                ["rejected"] = all.Count(a => a.ApprovalStatus == ApprovalStatuses.Rejected),
            };

            Applications = Show switch
            {
                "attention" => all.Where(Attention).ToList(),
                "unpaid" => all.Where(a => !a.IsPaid && a.ApprovalStatus != ApprovalStatuses.Rejected).ToList(),
                "paid" => all.Where(a => a.IsPaid).ToList(),
                "flagged" => all.Where(a => a.HasOpenFlags).ToList(),
                "rejected" => all.Where(a => a.ApprovalStatus == ApprovalStatuses.Rejected).ToList(),
                _ => all
            };
        }
    }
}

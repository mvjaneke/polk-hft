using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminMembership
{
    public class RemindersModel : MembershipAdminPage
    {
        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;
        private readonly MembershipReminderService _reminders;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public RemindersModel(ApplicationDbContext context, MembershipService membership, MembershipReminderService reminders, IWebHostEnvironment env, IConfiguration config)
        {
            _context = context;
            _membership = membership;
            _reminders = reminders;
            _env = env;
            _config = config;
        }

        public bool Enabled { get; private set; }
        public bool AutoRunBlockedHere { get; private set; }
        public string? LastRun { get; private set; }
        public ReminderPlan Plan { get; private set; } = new();
        public string? SampleEmail { get; private set; }
        public List<MembershipReminderLog> Log { get; private set; } = new();

        public async Task OnGetAsync()
        {
            Enabled = string.Equals(await _membership.GetSettingAsync(MembershipService.SettingKeys.RemindersEnabled), "true", StringComparison.OrdinalIgnoreCase);
            AutoRunBlockedHere = _env.IsDevelopment() && !_config.GetValue<bool>("Jobs:AutoRun");
            LastRun = await _membership.GetSettingAsync(MembershipService.SettingKeys.RemindersLastRun);
            Plan = await _reminders.BuildPlanAsync();

            if (Plan.EndingYear != null)
            {
                var next = Plan.NextYear ?? new ShootingYear
                {
                    Name = "(next year)", IndividualFee = Plan.EndingYear.IndividualFee,
                    FamilyFee = Plan.EndingYear.FamilyFee, PensionerFee = Plan.EndingYear.PensionerFee
                };
                var sample = Plan.Recipients.FirstOrDefault() ?? new ReminderRecipient
                {
                    EmailAddress = "member@example.com",
                    Members = { new ClubMember { FirstName = "Sam", Surname = "Member", IdNumber = "" } }
                };
                SampleEmail = MembershipReminderService.BuildEmail(sample, Plan.EndingYear, next, await _membership.GetSiteBaseUrlAsync());
            }

            Log = await _context.MembershipReminderLogs
                .Include(r => r.ClubMember)
                .Include(r => r.ShootingYear)
                .OrderByDescending(r => r.SentAt)
                .Take(100)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostToggleAsync(bool enable)
        {
            await _membership.SetSettingAsync(MembershipService.SettingKeys.RemindersEnabled, enable ? "true" : "false");
            await _context.SaveChangesAsync();
            Message = enable
                ? "Automatic reminders are on. They go out once per member, from the reminder date until the year ends."
                : "Automatic reminders are off. Nothing is sent unless you press Send now.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendNowAsync()
        {
            var result = await _reminders.SendAsync("Admin");
            if (result.Sent == 0) ErrorMessage = result.Summary;
            else Message = result.Summary;
            return RedirectToPage();
        }
    }
}

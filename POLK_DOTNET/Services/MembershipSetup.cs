using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    /// <summary>
    /// Runs at startup, after migrations. Everything here only fills in what is missing, so it
    /// is safe on every restart and never overwrites anything the admin has changed.
    /// </summary>
    public static class MembershipSetup
    {
        public static async Task EnsureAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var membership = services.GetRequiredService<MembershipService>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("MembershipSetup");

            // 1. Shooting years. Fees as on the club's 2026 paper form; the admin edits them from here.
            if (!await context.ShootingYears.AnyAsync())
            {
                context.ShootingYears.AddRange(
                    NewYear("2025/2026", 2025),
                    NewYear("2026/2027", 2026));
                await context.SaveChangesAsync();
                logger.LogInformation("Membership: seeded shooting years 2025/2026 and 2026/2027");
            }

            // 2. EFT details, from the paper form.
            var defaults = new Dictionary<string, string>
            {
                [MembershipService.SettingKeys.AccountName] = "POLK / PEAC HFT Klub",
                [MembershipService.SettingKeys.BankName] = "Capitec",
                [MembershipService.SettingKeys.AccountNumber] = "2347926876",
                [MembershipService.SettingKeys.BranchCode] = "470010",
                [MembershipService.SettingKeys.ProofOfPaymentEmail] = MembershipService.AccountsEmail,
                [MembershipService.SettingKeys.RemindersEnabled] = "false",
            };
            foreach (var (key, value) in defaults)
            {
                if (!await context.SiteSettings.AnyAsync(s => s.Key == key))
                    context.SiteSettings.Add(new SiteSettings { Key = key, Value = value });
            }
            await context.SaveChangesAsync();

            // 3. Applications from before shooting years existed: file them under the year they
            //    were submitted in, and put the people on paid ones on the member list.
            var years = await context.ShootingYears.ToListAsync();
            var undated = await context.MembershipApplications.Where(a => a.ShootingYearId == null).ToListAsync();
            foreach (var app in undated)
            {
                var local = DateTime.SpecifyKind(app.SubmittedDate, DateTimeKind.Utc).AddHours(2);
                var year = years.FirstOrDefault(y => y.Contains(local));
                if (year != null) app.ShootingYearId = year.Id;
            }
            await context.SaveChangesAsync();

            var paidUnlisted = await context.MembershipApplications
                .Include(a => a.Members)
                .Where(a => a.PaymentStatus == PaymentStatuses.Paid && a.ApprovalStatus != ApprovalStatuses.Rejected)
                .Where(a => a.Members.Any(m => m.ClubMemberId == null))
                .OrderBy(a => a.SubmittedDate)
                .ToListAsync();
            foreach (var app in paidUnlisted)
            {
                await membership.RecordMembershipAsync(app, "Online");
                await context.SaveChangesAsync();
            }
            if (paidUnlisted.Count > 0)
                logger.LogInformation("Membership: added people from {Count} earlier paid application(s) to the member list", paidUnlisted.Count);
        }

        private static ShootingYear NewYear(string name, int startYear) => new()
        {
            Name = name,
            StartDate = new DateTime(startYear, 11, 1),
            EndDate = new DateTime(startYear + 1, 10, 31),
            IndividualFee = 450m,
            FamilyFee = 750m,
            PensionerFee = 300m,
            NewMemberAdminFee = 250m,
            SahftaAdultFee = 180m,
            SahftaJuniorFee = 120m,
            RenewalReminderDaysBefore = 10
        };
    }
}

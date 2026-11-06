using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public class ReminderRecipient
    {
        public string EmailAddress { get; set; } = "";
        public List<ClubMember> Members { get; set; } = new();
        public string Names => string.Join(", ", Members.Select(m => m.FullName));
        public string GreetingName => Members.Count == 1 ? Members[0].FirstName : "POLK member";
    }

    public class ReminderPlan
    {
        /// <summary>The year that is ending, if today is inside its reminder window.</summary>
        public ShootingYear? EndingYear { get; set; }
        public ShootingYear? NextYear { get; set; }

        /// <summary>Why nothing will be sent, in words for the admin. Null when there is work to do.</summary>
        public string? Blocker { get; set; }

        public List<ReminderRecipient> Recipients { get; set; } = new();

        /// <summary>Paid for the ending year, not renewed, but no email address to write to.</summary>
        public List<ClubMember> MissingEmail { get; set; } = new();

        /// <summary>Email failed three times; fix the address on the member page.</summary>
        public List<ClubMember> FailedRepeatedly { get; set; } = new();

        /// <summary>Already reminded for this year: they are not reminded again.</summary>
        public int AlreadyReminded { get; set; }
    }

    /// <summary>
    /// Works out who needs a renewal reminder and sends it. One reminder per member per ending
    /// year, recorded in <see cref="MembershipReminderLog"/>, so it is safe to run as often as
    /// you like. People sharing an email address (a family) get a single email naming them all.
    /// </summary>
    public class MembershipReminderService
    {
        private static readonly SemaphoreSlim RunLock = new(1, 1);

        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;
        private readonly EmailService _email;
        private readonly ILogger<MembershipReminderService> _logger;

        public MembershipReminderService(ApplicationDbContext context, MembershipService membership, EmailService email, ILogger<MembershipReminderService> logger)
        {
            _context = context;
            _membership = membership;
            _email = email;
            _logger = logger;
        }

        public async Task<ReminderPlan> BuildPlanAsync(DateTime? today = null)
        {
            var d = (today ?? ClubClock.Today).Date;
            var plan = new ReminderPlan();

            var current = await _membership.GetCurrentYearAsync(d);
            if (current == null)
            {
                plan.Blocker = "No shooting year covers today's date. Add one on the Shooting Years page.";
                return plan;
            }

            plan.EndingYear = current;
            plan.NextYear = await _membership.GetFollowingYearAsync(current);

            if (d < current.RenewalOpensOn)
            {
                plan.Blocker = $"Reminders for {current.Name} start on {current.RenewalOpensOn:d MMMM yyyy}, {current.RenewalReminderDaysBefore} days before the year ends on {current.EndDate:d MMMM yyyy}.";
                // Still list who would get one, so the admin can tidy up email addresses in advance.
            }
            else if (plan.NextYear == null)
            {
                plan.Blocker = $"The next shooting year hasn't been set up yet. Add it on the Shooting Years page. Until it exists, the Apply page would still sell {current.Name}, which is about to end, so no reminders are sent.";
            }

            var nextId = plan.NextYear?.Id ?? -1;

            var candidates = await _context.ClubMembers
                .Where(m => !m.IsArchived)
                .Where(m => m.Years.Any(y => y.ShootingYearId == current.Id))
                .Where(m => !m.Years.Any(y => y.ShootingYearId == nextId))
                .OrderBy(m => m.Surname).ThenBy(m => m.FirstName)
                .ToListAsync();

            var remindedIds = await _context.MembershipReminderLogs
                .Where(r => r.ShootingYearId == current.Id && r.Success)
                .Select(r => r.ClubMemberId)
                .Distinct()
                .ToListAsync();

            // An address that has failed three times is not retried every hour for the rest of
            // the window; it shows up for the admin to correct instead.
            var givenUpIds = (await _context.MembershipReminderLogs
                    .Where(r => r.ShootingYearId == current.Id && !r.Success)
                    .Select(r => r.ClubMemberId)
                    .ToListAsync())
                .GroupBy(x => x).Where(g => g.Count() >= 3).Select(g => g.Key).ToHashSet();

            plan.AlreadyReminded = candidates.Count(c => remindedIds.Contains(c.Id));
            plan.FailedRepeatedly = candidates.Where(c => givenUpIds.Contains(c.Id) && !remindedIds.Contains(c.Id)).ToList();
            remindedIds.AddRange(givenUpIds);

            var due = candidates.Where(c => !remindedIds.Contains(c.Id)).ToList();
            plan.MissingEmail = due.Where(c => string.IsNullOrWhiteSpace(c.EmailAddress)).ToList();
            plan.Recipients = due
                .Where(c => !string.IsNullOrWhiteSpace(c.EmailAddress))
                .GroupBy(c => c.EmailAddress!.Trim().ToLowerInvariant())
                .Select(g => new ReminderRecipient { EmailAddress = g.First().EmailAddress!.Trim(), Members = g.ToList() })
                .OrderBy(r => r.Members[0].Surname)
                .ToList();

            return plan;
        }

        public record RunResult(int Sent, int Failed, string Summary);

        /// <summary>Sends reminders for today's plan. Does nothing while the plan has a blocker.</summary>
        public async Task<RunResult> SendAsync(string trigger, DateTime? today = null)
        {
            if (!await RunLock.WaitAsync(0))
                return new RunResult(0, 0, "A reminder run is already in progress.");

            try
            {
                var plan = await BuildPlanAsync(today);
                if (plan.Blocker != null || plan.EndingYear == null || plan.NextYear == null)
                    return await Finish(new RunResult(0, 0, plan.Blocker ?? "Nothing to send."));

                var baseUrl = await _membership.GetSiteBaseUrlAsync();
                int sent = 0, failed = 0;

                foreach (var recipient in plan.Recipients)
                {
                    var ok = await _email.SendEmailAsync(
                        recipient.EmailAddress,
                        $"Time to renew your POLK membership for {plan.NextYear.Name}",
                        BuildEmail(recipient, plan.EndingYear, plan.NextYear, baseUrl));

                    foreach (var member in recipient.Members)
                    {
                        _context.MembershipReminderLogs.Add(new MembershipReminderLog
                        {
                            ClubMemberId = member.Id,
                            ShootingYearId = plan.EndingYear.Id,
                            EmailAddress = recipient.EmailAddress,
                            SentAt = DateTime.UtcNow,
                            Success = ok,
                            Trigger = trigger
                        });
                    }
                    await _context.SaveChangesAsync();

                    if (ok) sent++; else failed++;
                }

                var summary = $"{ClubClock.Now:d MMM yyyy HH:mm}: {sent} reminder email(s) sent for {plan.EndingYear.Name}"
                              + (failed > 0 ? $", {failed} failed (they will be retried on the next run)" : "")
                              + (plan.MissingEmail.Count > 0 ? $", {plan.MissingEmail.Count} member(s) have no email address" : "")
                              + ".";
                return await Finish(new RunResult(sent, failed, summary));
            }
            finally
            {
                RunLock.Release();
            }

            async Task<RunResult> Finish(RunResult result)
            {
                await _membership.SetSettingAsync(MembershipService.SettingKeys.RemindersLastRun, $"{ClubClock.Now:d MMM yyyy HH:mm} ({trigger}): {result.Summary}");
                await _context.SaveChangesAsync();
                _logger.LogInformation("Renewal reminders ({Trigger}): {Summary}", trigger, result.Summary);
                return result;
            }
        }

        public static string BuildEmail(ReminderRecipient recipient, ShootingYear ending, ShootingYear next, string baseUrl)
        {
            static string H(string? s) => MembershipService.H(s);
            var who = recipient.Members.Count == 1
                ? "Your POLK membership"
                : $"The POLK membership for {H(recipient.Names)}";

            return $@"
                <h2>Time to renew your POLK membership</h2>
                <p>Dear {H(recipient.GreetingName)},</p>
                <p>{who} for <strong>{H(ending.Name)}</strong> ends on <strong>{ending.EndDate:d MMMM yyyy}</strong>.</p>
                <p>You can renew for <strong>{H(next.Name)}</strong> now. Choose <em>Renewal</em> on the form; there is no admin fee for renewals.</p>
                <table style='border-collapse:collapse;min-width:260px'>
                    <tr><td>Individual</td><td style='text-align:right'>R{next.IndividualFee:F2}</td></tr>
                    <tr><td>Family</td><td style='text-align:right'>R{next.FamilyFee:F2}</td></tr>
                    <tr><td>Pensioner</td><td style='text-align:right'>R{next.PensionerFee:F2}</td></tr>
                </table>
                <p style='margin:20px 0'><a href='{H(baseUrl)}/apply' style='background:#b91c1c;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;font-weight:bold'>Renew my membership</a></p>
                <p>If you have already renewed, thank you, and please ignore this email.</p>
                <p>Kind regards,<br/>Pretoria Oos Luggeweer Klub</p>";
        }
    }

    /// <summary>
    /// Runs the renewal reminders once an hour while the admin has switched them on. The
    /// per-member log makes a run on a day already handled a no-op, and a day the site was
    /// asleep is simply caught up on the next run. In Development it never sends on its own
    /// unless Jobs:AutoRun is set, so a local copy of the live database can't email members.
    /// </summary>
    public class MembershipReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<MembershipReminderBackgroundService> _logger;

        public MembershipReminderBackgroundService(IServiceScopeFactory scopes, IHostEnvironment env, IConfiguration config, ILogger<MembershipReminderBackgroundService> logger)
        {
            _scopes = scopes;
            _env = env;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_env.IsDevelopment() && !_config.GetValue<bool>("Jobs:AutoRun"))
            {
                _logger.LogInformation("Renewal reminders: automatic sending is off in Development (set Jobs:AutoRun to enable).");
                return;
            }

            // Let startup (migrations, seeding) finish first.
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var membership = scope.ServiceProvider.GetRequiredService<MembershipService>();
                    var enabled = string.Equals(await membership.GetSettingAsync(MembershipService.SettingKeys.RemindersEnabled), "true", StringComparison.OrdinalIgnoreCase);

                    // Office hours only, so nobody gets a club email at 2am.
                    var hour = ClubClock.Now.Hour;
                    if (enabled && hour >= 8 && hour < 18)
                    {
                        var reminders = scope.ServiceProvider.GetRequiredService<MembershipReminderService>();
                        var plan = await reminders.BuildPlanAsync();
                        if (plan.Blocker == null && plan.Recipients.Count > 0)
                            await reminders.SendAsync("Automatic");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Renewal reminder run failed");
                }

                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }
}

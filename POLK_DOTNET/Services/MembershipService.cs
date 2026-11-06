using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    /// <summary>Today's date in South Africa, whatever time zone the host runs in.</summary>
    public static class ClubClock
    {
        private static readonly TimeZoneInfo? Zone = Find("South Africa Standard Time") ?? Find("Africa/Johannesburg");

        private static TimeZoneInfo? Find(string id)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { return null; }
        }

        public static DateTime Now => Zone == null ? DateTime.UtcNow.AddHours(2) : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
        public static DateTime Today => Now.Date;
    }

    public record FeeBreakdown(decimal MembershipFee, decimal AdminFee)
    {
        public decimal Total => MembershipFee + AdminFee;
    }

    public class PaymentDetails
    {
        public string AccountName { get; set; } = "";
        public string BankName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string BranchCode { get; set; } = "";
        public string ProofOfPaymentEmail { get; set; } = "";
    }

    /// <summary>
    /// Everything about membership that more than one page needs: which year the Apply page
    /// sells, what it costs, what the admin should double-check, and what happens when an
    /// application is paid. Payment can arrive from the iKhokha webhook, the Yoco webhook, the
    /// payment-result page or the admin's "Mark paid" button; all of them come through
    /// <see cref="MarkPaidAsync"/> so they behave the same and a second arrival does nothing.
    /// </summary>
    public class MembershipService
    {
        public const string AccountsEmail = "accounts@polk-hft.co.za";

        public static class SettingKeys
        {
            public const string AccountName = "Membership:AccountName";
            public const string BankName = "Membership:BankName";
            public const string AccountNumber = "Membership:AccountNumber";
            public const string BranchCode = "Membership:BranchCode";
            public const string ProofOfPaymentEmail = "Membership:ProofOfPaymentEmail";
            public const string RemindersEnabled = "Membership:RemindersEnabled";
            public const string RemindersLastRun = "Membership:RemindersLastRun";
            public const string SiteBaseUrl = "Site:BaseUrl";
        }

        private readonly ApplicationDbContext _context;
        private readonly EmailService _email;
        private readonly ApplicationPdfService _pdf;
        private readonly ILogger<MembershipService> _logger;

        public MembershipService(ApplicationDbContext context, EmailService email, ApplicationPdfService pdf, ILogger<MembershipService> logger)
        {
            _context = context;
            _email = email;
            _pdf = pdf;
            _logger = logger;
        }

        // ---------------------------------------------------------------- years

        public async Task<ShootingYear?> GetCurrentYearAsync(DateTime? today = null)
        {
            var d = (today ?? ClubClock.Today).Date;
            return await _context.ShootingYears
                .Where(y => y.StartDate <= d && y.EndDate >= d)
                .OrderBy(y => y.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<ShootingYear?> GetFollowingYearAsync(ShootingYear year)
        {
            return await _context.ShootingYears
                .Where(y => y.StartDate > year.EndDate)
                .OrderBy(y => y.StartDate)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// The year a new application pays for. Normally the current year; from the day renewal
        /// reminders start (N days before the year ends) it is the following year, if the admin
        /// has set it up. Before any year has started, the first upcoming one.
        /// </summary>
        public async Task<ShootingYear?> GetYearForApplicationsAsync(DateTime? today = null)
        {
            var d = (today ?? ClubClock.Today).Date;
            var current = await GetCurrentYearAsync(d);
            if (current != null)
            {
                if (d >= current.RenewalOpensOn)
                {
                    var next = await GetFollowingYearAsync(current);
                    if (next != null) return next;
                }
                return current;
            }

            return await _context.ShootingYears
                .Where(y => y.StartDate > d)
                .OrderBy(y => y.StartDate)
                .FirstOrDefaultAsync();
        }

        public static FeeBreakdown Price(ShootingYear year, string? membershipType, string? applicationType) =>
            new(year.FeeFor(membershipType), applicationType == ApplicationTypes.New ? year.NewMemberAdminFee : 0m);

        // ---------------------------------------------------------------- checks for the admin

        /// <summary>
        /// Compares what the applicant said (new or renewal) with the member list. Nothing here
        /// blocks an application: the list is still being compiled from paper records, so a
        /// mismatch is something for the admin to look at, not proof of a mistake.
        /// </summary>
        public async Task<string?> BuildFlagsAsync(ShootingYear year, string applicationType, IEnumerable<(string Name, string IdNumber)> people)
        {
            var lines = new List<string>();
            foreach (var (name, rawId) in people)
            {
                var id = ClubMember.NormaliseIdNumber(rawId);
                var existing = await _context.ClubMembers
                    .Include(m => m.Years).ThenInclude(y => y.ShootingYear)
                    .Where(m => m.IdNumber == id)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    if (applicationType == ApplicationTypes.Renewal)
                        lines.Add($"Renewal: {name} (ID {id}) is not on the member list yet. Check the paper records; if they were a member, no admin fee is due.");
                    continue;
                }

                var paidYears = existing.Years
                    .OrderBy(y => y.ShootingYear.StartDate)
                    .Select(y => y.ShootingYear.Name)
                    .ToList();

                if (existing.Years.Any(y => y.ShootingYearId == year.Id))
                    lines.Add($"{name} is already paid up for {year.Name}. This may be a duplicate payment.");
                else if (applicationType == ApplicationTypes.New)
                    lines.Add(paidYears.Count > 0
                        ? $"New: {name} is already on the member list (paid {string.Join(", ", paidYears)}). They were charged the admin fee; this may be a renewal."
                        : $"New: {name} is already on the member list. They were charged the admin fee; this may be a renewal.");
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        // ---------------------------------------------------------------- payment

        /// <summary>
        /// Marks an application paid and puts everyone on it on the member list for its year.
        /// Returns false when it was already paid (or another request got there first), so
        /// callers never send the confirmation emails twice.
        /// </summary>
        /// <summary>Ids of unpaid duplicates the last MarkPaidAsync call closed.</summary>
        public List<int> LastClosedDuplicates { get; private set; } = new();

        public async Task<bool> MarkPaidAsync(int applicationId, string source, DateTime? paidDate = null, decimal? amount = null, bool sendEmails = true)
        {
            var application = await _context.MembershipApplications
                .Include(a => a.Members)
                .Include(a => a.ShootingYear)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
            {
                _logger.LogWarning("Membership payment for unknown application {Id} ({Source})", applicationId, source);
                return false;
            }

            if (application.IsPaid)
            {
                _logger.LogInformation("Application {Id} already paid; ignoring repeat from {Source}", applicationId, source);
                return false;
            }

            application.PaymentStatus = PaymentStatuses.Paid;
            application.PaidDate = paidDate ?? ClubClock.Now;
            application.AmountPaid = amount ?? application.TotalAmount;

            // Money arrived for an application that had been closed as a duplicate or abandoned
            // (e.g. a late EFT): it counts after all, so put it back in the admin's queue.
            if (application.IsClosed)
            {
                application.ApprovalStatus = ApprovalStatuses.Pending;
                application.AddNote($"{ClubClock.Today:d MMM yyyy}: payment received ({source}), so reopened.");
            }

            await RecordMembershipAsync(application, source);
            LastClosedDuplicates = await CloseDuplicatesOfAsync(application);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Webhook and return page racing each other: the unique (member, year) index
                // stops the second one, which is exactly what we want.
                _logger.LogWarning(ex, "Application {Id} payment from {Source} lost a race; already recorded", applicationId, source);
                _context.ChangeTracker.Clear();
                return false;
            }

            _logger.LogInformation("Application {Id} marked paid via {Source}", applicationId, source);

            if (sendEmails)
                await SendPaidEmailsAsync(application);

            return true;
        }

        /// <summary>
        /// Once an application is paid, earlier unpaid attempts by the same people for the same
        /// year are leftovers (typically a failed card payment followed by a fresh application),
        /// so close them instead of leaving them in the "needs action" queue. Only applications
        /// whose people are all covered by the paid one are closed, so a family application isn't
        /// closed because one of its members also applied alone. Does not save.
        /// </summary>
        public async Task<List<int>> CloseDuplicatesOfAsync(MembershipApplication paid)
        {
            var closed = new List<int>();
            if (paid.ShootingYearId == null) return closed;

            var paidIds = paid.Members
                .Select(m => ClubMember.NormaliseIdNumber(m.IdNumber))
                .Where(id => id.Length > 0)
                .ToHashSet();
            if (paidIds.Count == 0) return closed;

            var candidates = await _context.MembershipApplications
                .Include(a => a.Members)
                .Where(a => a.Id != paid.Id
                            && a.ShootingYearId == paid.ShootingYearId
                            && a.PaymentStatus != PaymentStatuses.Paid
                            && a.ApprovalStatus != ApprovalStatuses.Rejected
                            && a.ApprovalStatus != ApprovalStatuses.Closed)
                .ToListAsync();

            foreach (var other in candidates)
            {
                var ids = other.Members.Select(m => ClubMember.NormaliseIdNumber(m.IdNumber)).Where(id => id.Length > 0).ToList();
                if (ids.Count == 0 || !ids.All(paidIds.Contains)) continue;

                other.ApprovalStatus = ApprovalStatuses.Closed;
                other.AddNote($"{ClubClock.Today:d MMM yyyy}: closed automatically as a duplicate of #{paid.Id}, which was paid.");
                closed.Add(other.Id);
            }

            if (closed.Count > 0)
                _logger.LogInformation("Application {Id} paid; closed unpaid duplicates {Closed}", paid.Id, string.Join(", ", closed));
            return closed;
        }

        /// <summary>
        /// Matches each person on the application to the member list by ID number (creating them
        /// if new), refreshes their details from the form, and records the year as paid.
        /// Does not save.
        /// </summary>
        public async Task RecordMembershipAsync(MembershipApplication application, string source)
        {
            var primaryEmail = application.Members.FirstOrDefault(m => m.IsPrimary)?.EmailAddress
                               ?? application.Members.FirstOrDefault()?.EmailAddress;

            foreach (var person in application.Members)
            {
                var id = ClubMember.NormaliseIdNumber(person.IdNumber);

                ClubMember? member = null;
                if (person.ClubMemberId.HasValue)
                    member = await _context.ClubMembers.FindAsync(person.ClubMemberId.Value);
                member ??= _context.ClubMembers.Local.FirstOrDefault(m => m.IdNumber == id)
                           ?? await _context.ClubMembers.FirstOrDefaultAsync(m => m.IdNumber == id);

                if (member == null)
                {
                    member = new ClubMember { IdNumber = id, CreatedAt = DateTime.UtcNow };
                    _context.ClubMembers.Add(member);
                }

                member.FirstName = person.FirstName;
                member.Surname = person.Surname;
                member.Gender = NullIfEmpty(person.Gender) ?? member.Gender;
                member.ContactNumber = NullIfEmpty(person.ContactNumber) ?? member.ContactNumber;
                member.EmailAddress = NullIfEmpty(person.EmailAddress) ?? member.EmailAddress ?? NullIfEmpty(primaryEmail);
                member.SahftaNumber = NullIfEmpty(person.SahftaNumber) ?? member.SahftaNumber;
                member.IsJunior = person.IsJunior;
                member.StreetAddress = application.StreetAddress;
                member.ComplexOrBuilding = application.ComplexOrBuilding;
                member.Suburb = application.Suburb;
                member.City = application.City;
                member.Province = application.Province;
                member.PostalCode = application.PostalCode;
                if (application.SocialMediaConsent.HasValue)
                    member.SocialMediaConsent = application.SocialMediaConsent;
                member.IsArchived = false;
                member.UpdatedAt = DateTime.UtcNow;

                person.ClubMember = member;

                if (application.ShootingYearId is int yearId)
                {
                    var hasYear = member.Id != 0 && await _context.MemberYears
                        .AnyAsync(y => y.ClubMemberId == member.Id && y.ShootingYearId == yearId);
                    hasYear = hasYear || _context.MemberYears.Local.Any(y => y.ClubMember == member && y.ShootingYearId == yearId);

                    if (!hasYear)
                    {
                        _context.MemberYears.Add(new MemberYear
                        {
                            ClubMember = member,
                            ShootingYearId = yearId,
                            MembershipType = application.MembershipType,
                            MembershipApplicationId = application.Id,
                            PaidDate = application.PaidDate,
                            Source = source
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Takes back the paid years this application created, e.g. when the admin rejects an
        /// application that had already been paid. People stay on the list. Does not save.
        /// </summary>
        public async Task RemoveYearsFromApplicationAsync(int applicationId)
        {
            var years = await _context.MemberYears.Where(y => y.MembershipApplicationId == applicationId).ToListAsync();
            _context.MemberYears.RemoveRange(years);
        }

        // ---------------------------------------------------------------- settings

        public async Task<string?> GetSettingAsync(string key) =>
            (await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key))?.Value;

        public async Task SetSettingAsync(string key, string value)
        {
            var row = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (row == null) _context.SiteSettings.Add(new SiteSettings { Key = key, Value = value });
            else row.Value = value;
        }

        public async Task<PaymentDetails> GetPaymentDetailsAsync()
        {
            var keys = new[] { SettingKeys.AccountName, SettingKeys.BankName, SettingKeys.AccountNumber, SettingKeys.BranchCode, SettingKeys.ProofOfPaymentEmail };
            var values = await _context.SiteSettings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, s => s.Value);
            string V(string k) => values.TryGetValue(k, out var v) ? v : "";
            return new PaymentDetails
            {
                AccountName = V(SettingKeys.AccountName),
                BankName = V(SettingKeys.BankName),
                AccountNumber = V(SettingKeys.AccountNumber),
                BranchCode = V(SettingKeys.BranchCode),
                ProofOfPaymentEmail = V(SettingKeys.ProofOfPaymentEmail)
            };
        }

        public async Task<string> GetSiteBaseUrlAsync() =>
            (NullIfEmpty(await GetSettingAsync(SettingKeys.SiteBaseUrl)) ?? "https://www.polk-hft.co.za").TrimEnd('/');

        public static string PaymentReference(MembershipApplication application)
        {
            var surname = application.Members.FirstOrDefault(m => m.IsPrimary)?.Surname
                          ?? application.Members.FirstOrDefault()?.Surname ?? "";
            return $"APP{application.Id} {surname}".Trim();
        }

        // ---------------------------------------------------------------- emails

        public async Task SendSubmittedEmailsAsync(MembershipApplication application, string confirmationUrl)
        {
            var bank = await GetPaymentDetailsAsync();
            var primary = application.Members.FirstOrDefault(m => m.IsPrimary) ?? application.Members.FirstOrDefault();
            var summary = SummaryHtml(application);

            var flagsHtml = application.FlagLines.Any()
                ? "<h3 style='color:#92400e'>Please check</h3><ul>" + string.Join("", application.FlagLines.Select(f => $"<li>{H(f)}</li>")) + "</ul>"
                : "";

            await _email.SendEmailAsync(
                AccountsEmail,
                $"New Membership Application #{application.Id} - {application.ApplicationType} {application.MembershipType} ({application.ShootingYear?.Name})",
                $@"<h2>New Membership Application #{application.Id}</h2>
                   {flagsHtml}
                   {summary}
                   <p><strong>Payment method:</strong> {H(application.PaymentMethod == "EFT" ? "EFT (bank transfer)" : "Card (online)")}</p>
                   <p><strong>Payment status:</strong> Awaiting payment</p>");

            if (primary == null || string.IsNullOrWhiteSpace(primary.EmailAddress)) return;

            var eftHtml = application.PaymentMethod == "EFT"
                ? $@"<h3>How to pay</h3>
                     <p>Please pay <strong>R{application.TotalAmount:F2}</strong> by EFT to:</p>
                     <p>{H(bank.AccountName)}<br/>{H(bank.BankName)}<br/>Account: {H(bank.AccountNumber)}<br/>Branch code: {H(bank.BranchCode)}<br/>
                     Reference: <strong>{H(PaymentReference(application))}</strong></p>
                     <p>Then email the proof of payment to {H(bank.ProofOfPaymentEmail)}. Your membership is confirmed once the payment is received.</p>"
                : "<p>If your card payment did not go through, you can pay by EFT instead. The bank details are on your confirmation page.</p>";

            await _email.SendEmailAsync(
                primary.EmailAddress,
                $"Your POLK Membership Application #{application.Id}",
                $@"<h2>Membership application received</h2>
                   <p>Dear {H(primary.FirstName)},</p>
                   <p>Thank you for applying for POLK membership. A copy of your application is attached.</p>
                   {summary}
                   {eftHtml}
                   <p>You can see your application at any time here: <a href='{H(confirmationUrl)}'>{H(confirmationUrl)}</a></p>
                   <p>Kind regards,<br/>Pretoria Oos Luggeweer Klub</p>",
                PdfAttachment(application));
        }

        /// <summary>Whether the last payment confirmation actually reached the mail server.</summary>
        public bool LastConfirmationEmailSent { get; private set; }

        public async Task SendPaidEmailsAsync(MembershipApplication application)
        {
            LastConfirmationEmailSent = false;
            var primary = application.Members.FirstOrDefault(m => m.IsPrimary) ?? application.Members.FirstOrDefault();
            var name = primary != null ? $"{primary.FirstName} {primary.Surname}" : "Applicant";
            var year = application.ShootingYear?.Name ?? "";

            await _email.SendEmailAsync(
                AccountsEmail,
                $"Payment Received - Membership Application #{application.Id}",
                $@"<h2>Payment received: Application #{application.Id}</h2>
                   <p><strong>Applicant:</strong> {H(name)}</p>
                   <p><strong>Amount paid:</strong> R{(application.AmountPaid ?? application.TotalAmount):F2}</p>
                   {SummaryHtml(application)}
                   <p>Everyone on this application is now on the member list as paid up for {H(year)}.</p>");

            if (primary == null || string.IsNullOrWhiteSpace(primary.EmailAddress)) return;

            var endText = application.ShootingYear != null ? $" It runs until {application.ShootingYear.EndDate:d MMMM yyyy}." : "";
            LastConfirmationEmailSent = await _email.SendEmailAsync(
                primary.EmailAddress,
                $"Payment Confirmed - POLK Membership {year}",
                $@"<h2>Payment confirmed</h2>
                   <p>Dear {H(primary.FirstName)},</p>
                   <p>We have received your payment of <strong>R{(application.AmountPaid ?? application.TotalAmount):F2}</strong>.
                   Your POLK membership for <strong>{H(year)}</strong> is paid up.{H(endText)}</p>
                   {SummaryHtml(application)}
                   <p>Your updated application is attached for your records.</p>
                   <p>Kind regards,<br/>Pretoria Oos Luggeweer Klub</p>",
                PdfAttachment(application));
        }

        private List<EmailAttachment> PdfAttachment(MembershipApplication application) => new()
        {
            new EmailAttachment
            {
                FileName = $"MembershipApplication_{application.Id}.pdf",
                Content = _pdf.GenerateApplicationPdf(application),
                ContentType = "application/pdf"
            }
        };

        private static string SummaryHtml(MembershipApplication a)
        {
            var people = string.Join("", a.Members.Select(m =>
                $"<tr><td style='padding:6px;border:1px solid #ddd'>{H(m.FirstName)} {H(m.Surname)}{(m.IsJunior ? " (under 18)" : "")}</td>" +
                $"<td style='padding:6px;border:1px solid #ddd'>{H(m.IdNumber)}</td>" +
                $"<td style='padding:6px;border:1px solid #ddd'>{H(m.EmailAddress)}</td>" +
                $"<td style='padding:6px;border:1px solid #ddd'>{H(m.ContactNumber)}</td>" +
                $"<td style='padding:6px;border:1px solid #ddd'>{H(m.SahftaNumber ?? "")}</td></tr>"));

            var admin = a.AdminFee > 0 ? $"<tr><td>Once-off registration and admin fee</td><td style='text-align:right'>R{a.AdminFee:F2}</td></tr>" : "";

            return $@"
                <p><strong>Membership year:</strong> {H(a.ShootingYear?.Name ?? "")}<br/>
                   <strong>Application:</strong> {H(a.ApplicationType)} &middot; {H(a.MembershipType)}</p>
                <table style='border-collapse:collapse;min-width:280px'>
                    <tr><td>{H(a.MembershipType)} membership</td><td style='text-align:right'>R{a.MembershipFee:F2}</td></tr>
                    {admin}
                    <tr><td><strong>Total</strong></td><td style='text-align:right'><strong>R{a.TotalAmount:F2}</strong></td></tr>
                </table>
                <h3>People on this application</h3>
                <table style='border-collapse:collapse;width:100%'>
                    <tr style='background:#f2f2f2'>
                        <th style='padding:6px;border:1px solid #ddd;text-align:left'>Name</th>
                        <th style='padding:6px;border:1px solid #ddd;text-align:left'>ID number</th>
                        <th style='padding:6px;border:1px solid #ddd;text-align:left'>Email</th>
                        <th style='padding:6px;border:1px solid #ddd;text-align:left'>Contact</th>
                        <th style='padding:6px;border:1px solid #ddd;text-align:left'>SAHFTA no.</th>
                    </tr>
                    {people}
                </table>";
        }

        public static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

        public static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        public static string Money(decimal amount) => "R" + amount.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ");
    }
}

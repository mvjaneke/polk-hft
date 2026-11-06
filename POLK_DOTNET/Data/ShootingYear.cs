using System.ComponentModel.DataAnnotations;

namespace POLK_DOTNET.Data
{
    /// <summary>
    /// One club year (1 November to 31 October, the same as SAHFTA). Each year carries its
    /// own fees so a price change for next year never rewrites what this year cost, and
    /// every application keeps a copy of the amounts it was actually charged.
    /// </summary>
    public class ShootingYear
    {
        public int Id { get; set; }

        /// <summary>Shown to members, e.g. "2026/2027".</summary>
        [Required, StringLength(50)]
        public string Name { get; set; } = null!;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal IndividualFee { get; set; }
        public decimal FamilyFee { get; set; }
        public decimal PensionerFee { get; set; }

        /// <summary>Once-off registration and admin fee, charged once per new application.</summary>
        public decimal NewMemberAdminFee { get; set; }

        /// <summary>SAHFTA fees are paid directly to SAHFTA. Shown for information only.</summary>
        public decimal SahftaAdultFee { get; set; }
        public decimal SahftaJuniorFee { get; set; }

        /// <summary>
        /// How many days before the year ends renewal reminders go out. From the same day the
        /// Apply page takes applications for the following year, so a reminder never points
        /// someone at a form that would sell them the year that is about to end.
        /// </summary>
        public int RenewalReminderDaysBefore { get; set; } = 10;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Contains(DateTime date) => date.Date >= StartDate.Date && date.Date <= EndDate.Date;

        public DateTime RenewalOpensOn => EndDate.Date.AddDays(-RenewalReminderDaysBefore);

        public decimal FeeFor(string? membershipType) => membershipType switch
        {
            MembershipTypes.Individual => IndividualFee,
            MembershipTypes.Family => FamilyFee,
            MembershipTypes.Pensioner => PensionerFee,
            _ => 0m
        };
    }

    public static class MembershipTypes
    {
        public const string Individual = "Individual";
        public const string Family = "Family";
        public const string Pensioner = "Pensioner";

        public static readonly string[] All = { Individual, Family, Pensioner };
    }

    public static class ApplicationTypes
    {
        public const string New = "New";
        public const string Renewal = "Renewal";
    }

    public static class PaymentStatuses
    {
        public const string Unpaid = "Unpaid";
        public const string Paid = "Paid";
    }

    public static class ApprovalStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        /// <summary>Never paid and no longer expected to be: a duplicate attempt or an abandoned application.</summary>
        public const string Closed = "Closed";
    }
}

using System.ComponentModel.DataAnnotations;

namespace POLK_DOTNET.Data
{
    /// <summary>
    /// A person on the club's member list. One row per person, whatever the number of
    /// applications they make over the years. Which years they paid for lives in
    /// <see cref="MemberYear"/>, so "is this person paid up?" is always a question about
    /// a specific shooting year.
    /// </summary>
    /// <remarks>
    /// Named ClubMember because <see cref="Member"/> already exists: that is a person as
    /// written on one application form, kept as submitted.
    /// </remarks>
    public class ClubMember
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = null!;

        [Required, StringLength(100)]
        public string Surname { get; set; } = null!;

        /// <summary>Stored without spaces; this is how renewals are matched.</summary>
        [Required, StringLength(20)]
        public string IdNumber { get; set; } = null!;

        [StringLength(20)]
        public string? Gender { get; set; }

        [StringLength(20)]
        public string? ContactNumber { get; set; }

        [StringLength(100)]
        public string? EmailAddress { get; set; }

        [StringLength(30)]
        public string? SahftaNumber { get; set; }

        public bool IsJunior { get; set; }

        [StringLength(200)] public string? StreetAddress { get; set; }
        [StringLength(200)] public string? ComplexOrBuilding { get; set; }
        [StringLength(100)] public string? Suburb { get; set; }
        [StringLength(100)] public string? City { get; set; }
        [StringLength(50)] public string? Province { get; set; }
        [StringLength(10)] public string? PostalCode { get; set; }

        /// <summary>Latest answer to the social-media permission question. Null = never asked.</summary>
        public bool? SocialMediaConsent { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Hidden from the list and never sent reminders, but kept for history.</summary>
        public bool IsArchived { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MemberYear> Years { get; set; } = new List<MemberYear>();

        public string FullName => $"{FirstName} {Surname}".Trim();

        public static string NormaliseIdNumber(string? idNumber) =>
            new string((idNumber ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }

    /// <summary>A member paid up for one shooting year. One row per member per year.</summary>
    public class MemberYear
    {
        public int Id { get; set; }

        public int ClubMemberId { get; set; }
        public ClubMember ClubMember { get; set; } = null!;

        public int ShootingYearId { get; set; }
        public ShootingYear ShootingYear { get; set; } = null!;

        [StringLength(20)]
        public string? MembershipType { get; set; }

        /// <summary>The application that paid for this year. Null when the admin recorded it by hand.</summary>
        public int? MembershipApplicationId { get; set; }
        public MembershipApplication? MembershipApplication { get; set; }

        public DateTime? PaidDate { get; set; }

        /// <summary>Online, EFT, or Manual (entered by the admin, e.g. from paper records).</summary>
        [StringLength(20)]
        public string Source { get; set; } = "Manual";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// One row per renewal reminder sent to a member for the year that is ending. This is what
    /// stops a reminder going out twice, however often the job runs.
    /// </summary>
    public class MembershipReminderLog
    {
        public int Id { get; set; }

        public int ClubMemberId { get; set; }
        public ClubMember ClubMember { get; set; } = null!;

        /// <summary>The year that was ending when the reminder went out.</summary>
        public int ShootingYearId { get; set; }
        public ShootingYear ShootingYear { get; set; } = null!;

        [StringLength(100)]
        public string EmailAddress { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool Success { get; set; }

        /// <summary>"Automatic" or "Admin".</summary>
        [StringLength(20)]
        public string Trigger { get; set; } = "Automatic";
    }
}

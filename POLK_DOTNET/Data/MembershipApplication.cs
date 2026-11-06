using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc; // Required for ModelBinderAttribute
using POLK_DOTNET.CustomModelBinders; // Required for DecimalInvariantModelBinder

namespace POLK_DOTNET.Data
{
    public class MembershipApplication
    {
        public int Id { get; set; }

        [Required]
        public string MembershipType { get; set; } = null!; // Individual, Family, Pensioner

        [Required]
        [ModelBinder(typeof(DecimalInvariantModelBinder))]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(200)]
        public string StreetAddress { get; set; } = null!;

        [StringLength(200)]
        public string? ComplexOrBuilding { get; set; }

        [Required]
        [StringLength(100)]
        public string Suburb { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Province { get; set; } = null!;

        [Required]
        [StringLength(10)]
        public string PostalCode { get; set; } = null!;

        /// <summary>
        /// Legacy single status (Pending / Approved / Paid / Rejected). It mixed up payment and
        /// approval, so a payment overwrote an approval and vice versa. Kept only so old rows
        /// still read; use <see cref="PaymentStatus"/> and <see cref="ApprovalStatus"/>.
        /// </summary>
        public string Status { get; set; } = "Pending";

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

        /// <summary>New or Renewal, as the applicant said. New applications pay the admin fee.</summary>
        [StringLength(20)]
        public string ApplicationType { get; set; } = ApplicationTypes.New;

        /// <summary>The shooting year this application pays for.</summary>
        public int? ShootingYearId { get; set; }
        public ShootingYear? ShootingYear { get; set; }

        /// <summary>Copies of what was charged, so later fee changes never rewrite history.</summary>
        public decimal MembershipFee { get; set; }
        public decimal AdminFee { get; set; }

        /// <summary>Card (online) or EFT.</summary>
        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Card";

        [StringLength(20)]
        public string PaymentStatus { get; set; } = PaymentStatuses.Unpaid;

        public DateTime? PaidDate { get; set; }
        public decimal? AmountPaid { get; set; }

        [StringLength(20)]
        public string ApprovalStatus { get; set; } = ApprovalStatuses.Pending;

        /// <summary>Permission to publish photos on social media / marketing (paper form declaration).</summary>
        public bool? SocialMediaConsent { get; set; }

        /// <summary>Parent or legal guardian who accepted the declaration for anyone under 18.</summary>
        [StringLength(200)]
        public string? GuardianName { get; set; }

        /// <summary>
        /// Things the admin should check, one per line: e.g. a renewal whose ID number is not on
        /// the member list yet, or a "new" applicant who is already on it. Empty when nothing to check.
        /// </summary>
        [StringLength(2000)]
        public string? Flags { get; set; }

        public bool FlagsReviewed { get; set; }

        [StringLength(1000)]
        public string? AdminNotes { get; set; }

        /// <summary>Unguessable key for the applicant's confirmation page, so ids can't be enumerated.</summary>
        [StringLength(40)]
        public string AccessToken { get; set; } = Guid.NewGuid().ToString("N");

        public bool IsPaid => PaymentStatus == PaymentStatuses.Paid;
        public bool HasOpenFlags => !string.IsNullOrWhiteSpace(Flags) && !FlagsReviewed;
        public IEnumerable<string> FlagLines => (Flags ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public ICollection<Member> Members { get; set; } = new List<Member>();
    }

    public class Member
    {
        public int Id { get; set; }

        [Required]
        public int MembershipApplicationId { get; set; }
        public MembershipApplication MembershipApplication { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string IdNumber { get; set; } = null!;

        [StringLength(20)]
        public string Gender { get; set; } = null!; // Male, Female, Other

        [Required]
        [StringLength(20)]
        public string ContactNumber { get; set; } = null!;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string EmailAddress { get; set; } = null!;

        public bool IsPrimary { get; set; } = false; // True for the main contact in a family application

        /// <summary>Legacy: POLK used to collect SAHFTA fees. New applications leave these false/0.</summary>
        public bool HasSahftaAffiliation { get; set; } = false;
        public decimal SahftaFee { get; set; } = 0;

        /// <summary>Under 18. A parent or guardian accepts the declaration for them.</summary>
        public bool IsJunior { get; set; } = false;

        [StringLength(30)]
        public string? SahftaNumber { get; set; }

        /// <summary>The member-list entry this person was matched to or created as, once paid.</summary>
        public int? ClubMemberId { get; set; }
        public ClubMember? ClubMember { get; set; }
    }
}

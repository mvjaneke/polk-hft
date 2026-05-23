using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POLK_DOTNET.Data
{
    public class EventRegistration
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        [Required]
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Paid, Cancelled

        // Core Registration Fields
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string CellNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string IdNumber { get; set; } = string.Empty;

        // Per-event optional fields
        [StringLength(50)]
        public string? AttendanceType { get; set; } // Competitor | Spectator

        [StringLength(50)]
        public string? SAHFTANumber { get; set; }

        [StringLength(100)]
        public string? ClubName { get; set; }

        [StringLength(50)]
        public string? Division { get; set; }

        // For double-header events: "First" | "Second" | "Both". Null for single-shoot events.
        [StringLength(20)]
        public string? ShootSelection { get; set; }

        [StringLength(100)]
        public string? OtherDivision { get; set; }

        [Required]
        [StringLength(50)]
        public string GunType { get; set; } = string.Empty;

        // "Own" | "Club" (only when event.AllowsClubRifle)
        [StringLength(20)]
        public string? RifleOwnership { get; set; }

        // Guardian/Minor Fields
        [StringLength(100)]
        public string? GuardianName { get; set; }

        [StringLength(100)]
        public string? GuardianSurname { get; set; }

        // Acknowledgements/Indemnities
        [Required]
        public bool InfoAccurateConfirmed { get; set; }

        [Required]
        public bool IndemnityAgreed { get; set; }

        public bool GuardianIndemnityAgreed { get; set; }

        // "Agreed" | "GuardianAgreed" | "Declined"
        [StringLength(30)]
        public string? SocialMediaConsent { get; set; }

        // Payment Details
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountPaid { get; set; }

        [StringLength(100)]
        public string? PaymentReference { get; set; }

        // "Yoco" | "EFT" | "AtVenue"
        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        [StringLength(100)]
        public string? YocoCheckoutId { get; set; }

        [StringLength(100)]
        public string? YocoPaymentId { get; set; }

        public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    }
}

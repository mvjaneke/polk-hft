using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace POLK_DOTNET.Data
{
    // A booking for an event. One person (the contact) completes the form and pays, and
    // everyone attending under the booking — including the contact — is an EventParticipant.
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

        // --- Contact / payer ---

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

        // Meals booked for the whole party on top of the entries themselves (spectators,
        // family). Charged at Event.MealFee each — including to spectator-only bookings,
        // which pay no entry fee.
        public int ExtraMeals { get; set; }

        // --- Payment (per booking, covering every participant) ---

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

        [NotMapped]
        public IEnumerable<EventParticipant> Competitors =>
            Participants.Where(p => !p.IsSpectator).OrderBy(p => p.Position);

        // --- Legacy single-person columns ---
        // Kept for registrations captured before bookings supported multiple people. The
        // migration copies these into a first EventParticipant, and all live logic reads
        // Participants — nothing writes these any more.

        [StringLength(50)]
        public string? AttendanceType { get; set; }

        [StringLength(50)]
        public string? SAHFTANumber { get; set; }

        [StringLength(100)]
        public string? ClubName { get; set; }

        [StringLength(50)]
        public string? Division { get; set; }

        [StringLength(20)]
        public string? ShootSelection { get; set; }

        [StringLength(100)]
        public string? OtherDivision { get; set; }

        [StringLength(50)]
        public string GunType { get; set; } = string.Empty;

        [StringLength(20)]
        public string? RifleOwnership { get; set; }

        [StringLength(100)]
        public string? GuardianName { get; set; }

        [StringLength(100)]
        public string? GuardianSurname { get; set; }

        public bool InfoAccurateConfirmed { get; set; }

        public bool IndemnityAgreed { get; set; }

        public bool GuardianIndemnityAgreed { get; set; }

        [StringLength(30)]
        public string? SocialMediaConsent { get; set; }

        public int? StartingLaneShoot1 { get; set; }
        public int? StartingLaneShoot2 { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POLK_DOTNET.Data
{
    // One person on a booking. An EventRegistration is the booking (contact + payment);
    // every person attending under it — including the contact themselves — gets a row here.
    // All downstream logic (scorecards, starting lanes, score sheets, exports) reads
    // participants, so a solo entry is simply a booking with one participant.
    public class EventParticipant
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration EventRegistration { get; set; } = null!;

        // 1-based order within the booking. Position 1 is the contact person by default.
        public int Position { get; set; } = 1;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;

        [StringLength(20)]
        public string? IdNumber { get; set; }

        // "Competitor" | "Spectator"
        [StringLength(50)]
        public string? AttendanceType { get; set; }

        [StringLength(50)]
        public string GunType { get; set; } = string.Empty;

        // "Own" | "Club" (only when event.AllowsClubRifle)
        [StringLength(20)]
        public string? RifleOwnership { get; set; }

        [StringLength(50)]
        public string? Division { get; set; }

        [StringLength(100)]
        public string? OtherDivision { get; set; }

        [StringLength(50)]
        public string? SAHFTANumber { get; set; }

        [StringLength(100)]
        public string? ClubName { get; set; }

        // Double-header only: "First" | "Second" | "Both". Provincial two-day events enter
        // every competitor for both days, so this stays null there.
        [StringLength(20)]
        public string? ShootSelection { get; set; }

        [StringLength(100)]
        public string? GuardianName { get; set; }

        [StringLength(100)]
        public string? GuardianSurname { get; set; }

        // Each person signs for themselves — the contact can't indemnify the rest of the group.
        public bool InfoAccurateConfirmed { get; set; }
        public bool IndemnityAgreed { get; set; }
        public bool GuardianIndemnityAgreed { get; set; }

        // "Agreed" | "GuardianAgreed" | "Declined"
        [StringLength(30)]
        public string? SocialMediaConsent { get; set; }

        // Starting lane on the squadding sheet, imported from the "Indeling" spreadsheet.
        // Shoot2 covers the second round (double-header shoot 2, or provincial day 2).
        public int? StartingLaneShoot1 { get; set; }
        public int? StartingLaneShoot2 { get; set; }

        [NotMapped]
        public bool IsSpectator =>
            string.Equals(AttendanceType, "Spectator", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public string FullName => $"{Name} {Surname}".Trim();
    }
}

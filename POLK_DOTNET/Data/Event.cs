using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POLK_DOTNET.Data
{
    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Time { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Location { get; set; } = null!;
        public int? Participants { get; set; }
        public int? MaxParticipants { get; set; }
        public string? Color { get; set; }
        public bool IsClubEvent { get; set; } = false;

        // Registration settings
        public bool IsRegistrationOpen { get; set; } = false;
        public DateTime? RegistrationCloseDate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? EntryFee { get; set; }

        public string? EntryFeeDescription { get; set; }

        // "HFT" | "Benchrest" | "Other"
        public string EventType { get; set; } = "HFT";

        // Which optional sections to show on the public registration form
        public bool RequiresSahfta { get; set; } = false;
        public bool RequiresDivision { get; set; } = false;
        public bool RequiresClubName { get; set; } = false;
        public bool RequiresAttendanceType { get; set; } = false;
        public bool AllowsClubRifle { get; set; } = false;

        public string? BankingDetailsHtml { get; set; }

        public bool EnableYocoPayment { get; set; } = false;

        // Optional per-event notification email (fallback: accounts@polk-hft.co.za)
        public string? NotificationEmail { get; set; }

        // League-shoot gating (HFT only) — exposes Troyer course setup when true
        public bool IsLeagueShoot { get; set; } = false;

        // Selected Troyer variant: 30 or 40 (nullable = not yet configured)
        public int? CourseTargetCount { get; set; }
    }
}

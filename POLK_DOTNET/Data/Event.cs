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

        // Double-header: two league shoots held on the same day
        public bool IsDoubleHeader { get; set; } = false;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? DoubleHeaderFee { get; set; }

        // When true, scorecards for both shoots use the Shoot=1 course; admin only edits one course.
        public bool UseSameCourseForBothShoots { get; set; } = true;

        // Extra fee added once per registration when AllowsClubRifle and the registrant chose "Club".
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ClubRifleFee { get; set; }

        // Provincial gating (HFT) — a two-day provincial shoot. Everyone who enters shoots
        // both days, so there's no per-day choice on the registration form; the two days each
        // get their own course, starting lanes and scorecards (mirroring the double-header).
        public bool IsProvincialTwoDay { get; set; } = false;

        // Optional meal booking on the registration form — spectators, family, etc.
        public bool OffersMeals { get; set; } = false;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MealFee { get; set; }

        // What the meal is, shown next to the price (e.g. "Lunch pack — boerie roll & cold drink").
        public string? MealDescription { get; set; }

        // --- Derived helpers (never persisted) ---

        // True when the event is shot in two rounds: a league double-header (two shoots on one
        // day) or a provincial event (one shoot per day across two days).
        [NotMapped]
        public bool HasTwoRounds => IsDoubleHeader || IsProvincialTwoDay;

        // What a round is called in the admin UI, PDFs and exports.
        [NotMapped]
        public string RoundLabel => IsProvincialTwoDay && !IsDoubleHeader ? "Day" : "Shoot";

        // Troyer course setup applies to HFT league shoots and provincial events.
        [NotMapped]
        public bool UsesCourseSetup => EventType == "HFT" && (IsLeagueShoot || IsProvincialTwoDay);

        // Meals only cost money once a price has been captured.
        [NotMapped]
        public bool MealsChargeable => OffersMeals && MealFee.HasValue && MealFee.Value > 0;

        // The calendar date a given round falls on. Provincial day 2 uses EndDate when set.
        public DateTime RoundDate(int round) =>
            (round == 2 && IsProvincialTwoDay && EndDate.HasValue) ? EndDate.Value : StartDate;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    // Single source of truth for what a booking costs. Everything that quotes, charges,
    // collects or reconciles a registration goes through here.
    public static class EventFeeCalculator
    {
        // What one person costs to enter. Spectators pay nothing to attend — they only ever
        // owe for meals, which are booked per booking rather than per person. A provincial
        // two-day entry is a single fee covering both days, so only double-headers split.
        public static decimal ForParticipant(Event ev, EventParticipant p)
        {
            if (p.IsSpectator) return 0m;

            var single = ev.EntryFee ?? 0m;
            var entry = ev.IsDoubleHeader && string.Equals(p.ShootSelection, "Both", StringComparison.OrdinalIgnoreCase)
                ? ev.DoubleHeaderFee ?? single * 2
                : single;

            var rifle = ev.AllowsClubRifle && string.Equals(p.RifleOwnership, "Club", StringComparison.OrdinalIgnoreCase)
                ? ev.ClubRifleFee ?? 0m
                : 0m;

            return entry + rifle;
        }

        public static decimal EntriesTotal(Event ev, IEnumerable<EventParticipant> participants) =>
            participants.Sum(p => ForParticipant(ev, p));

        public static decimal MealsTotal(Event ev, int extraMeals) =>
            ev.MealsChargeable && extraMeals > 0 ? ev.MealFee!.Value * extraMeals : 0m;

        public static decimal ForBooking(Event ev, IEnumerable<EventParticipant> participants, int extraMeals) =>
            EntriesTotal(ev, participants) + MealsTotal(ev, extraMeals);

        // Requires reg.Participants to be loaded — callers reading from the database must
        // Include(r => r.Participants) or the total silently comes back as meals only.
        public static decimal ForBooking(Event ev, EventRegistration reg) =>
            ForBooking(ev, reg.Participants, reg.ExtraMeals);
    }
}

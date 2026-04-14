using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public static class TroyerCalculator
    {
        public const string Prone = "Prone";
        public const string UnStand = "UnStand";
        public const string UnKneel = "UnKneel";
        public const string SupStand = "SupStand";
        public const string SupKneel = "SupKneel";

        public static readonly string[] Postures = { Prone, UnStand, UnKneel, SupStand, SupKneel };

        public static string PostureLabel(string? p) => p switch
        {
            Prone => "Prone",
            UnStand => "Unsupported Standing",
            UnKneel => "Unsupported Kneeling",
            SupStand => "Supported Standing",
            SupKneel => "Supported Kneeling",
            _ => "Prone"
        };

        public class RowResult
        {
            public decimal DiffFactor { get; set; }
            public decimal DiffScore { get; set; }
            public string Rating { get; set; } = "";
            public List<string> Errors { get; set; } = new();
            public bool IsOk => Errors.Count == 0;
        }

        public class SummaryResult
        {
            public int TargetCount { get; set; }
            public int StandCount { get; set; }
            public int KneelCount { get; set; }
            public int RequiredStanding { get; set; }
            public int RequiredKneeling { get; set; }
            public int Count35_40 { get; set; }
            public int Count30_34 { get; set; }
            public int Count25_29 { get; set; }
            public int Count20_24 { get; set; }
            public int Count15_19 { get; set; }
            public int Count25to34 { get; set; }
            public int Required25to34Min { get; set; }
            public int Count15to19 { get; set; }
            public int Required15to19Min { get; set; }
            public int CountExp { get; set; }
            public int CountMod { get; set; }
            public int CountEasy { get; set; }
            public decimal AverageDistance { get; set; }
            public decimal AverageDifficulty { get; set; }
            public List<string> Errors { get; set; } = new();
            public bool IsOk => Errors.Count == 0;
        }

        // Killzone → allowed distance range (overall range, regardless of posture)
        // Mirrors the sheet's B61:E64 lookup
        private static (int fromMm, int toMm, int minM, int maxM)[] KillzoneRange =
        {
            (15, 19, 12, 23),
            (20, 24,  7, 28),
            (25, 34,  7, 37),
            (35, 40,  7, 42),
        };

        // Both unsupported and supported share the same 35–40mm range entry (7–32m) per the sheet
        private static (int fromMm, int toMm, int minM, int maxM) UnsupportedRange = (35, 40, 7, 32);
        private static (int fromMm, int toMm, int minM, int maxM) SupportedRange   = (35, 40, 7, 32);

        public static RowResult ComputeRow(CourseTarget t)
        {
            var r = new RowResult();

            // Diff factor
            decimal df = 1m;
            switch (t.Posture)
            {
                case UnStand: df += 0.89m; break;
                case UnKneel: df += 0.62m; break;
                case SupStand: df += 0.435m; break;
                case SupKneel: df += 0.325m; break;
            }
            if (t.IsInclineDecline) df += 0.1m;
            if (t.IsShaded) df += 0.1m;
            r.DiffFactor = Math.Round(df, 3);

            // Empty row (no killzone or no distance) → skip difficulty calc but still validate posture
            if (t.KillZoneMm > 0 && t.DistanceMeters > 0)
            {
                r.DiffScore = Math.Round(ComputeDifficulty(t.DistanceMeters, t.KillZoneMm, df), 2);
                r.Rating = r.DiffScore > 30 ? "Exp" : r.DiffScore > 25 ? "Mod" : "Easy";
            }

            // Validation checks (no error for unset posture — defaults to Prone)
            if (t.KillZoneMm > 0 && t.DistanceMeters > 0)
            {
                var range = LookupKillzoneRange(t.KillZoneMm);
                if (range == null)
                    r.Errors.Add($"Killzone {t.KillZoneMm}mm is outside 15–40mm");
                else if (t.DistanceMeters < range.Value.minM || t.DistanceMeters > range.Value.maxM)
                    r.Errors.Add($"Distance {t.DistanceMeters}m outside allowed {range.Value.minM}–{range.Value.maxM}m for {t.KillZoneMm}mm");

                if ((t.Posture == UnStand || t.Posture == UnKneel) && t.KillZoneMm >= 35)
                {
                    if (t.DistanceMeters < UnsupportedRange.minM || t.DistanceMeters > UnsupportedRange.maxM)
                        r.Errors.Add($"Unsupported {t.KillZoneMm}mm max distance is {UnsupportedRange.maxM}m");
                }
                if ((t.Posture == SupStand || t.Posture == SupKneel) && t.KillZoneMm >= 35)
                {
                    if (t.DistanceMeters < SupportedRange.minM || t.DistanceMeters > SupportedRange.maxM)
                        r.Errors.Add($"Supported {t.KillZoneMm}mm max distance is {SupportedRange.maxM}m");
                }
            }
            return r;
        }

        public static SummaryResult ComputeSummary(IList<CourseTarget> targets, int courseTargetCount)
        {
            var s = new SummaryResult { TargetCount = courseTargetCount };

            s.RequiredStanding = courseTargetCount == 30 ? 3 : 4;
            s.RequiredKneeling = courseTargetCount == 30 ? 3 : 4;
            s.Required25to34Min = 6;
            s.Required15to19Min = courseTargetCount == 30 ? 4 : 6;

            foreach (var t in targets)
            {
                if (t.Posture == UnStand || t.Posture == SupStand) s.StandCount++;
                if (t.Posture == UnKneel || t.Posture == SupKneel) s.KneelCount++;

                if (t.KillZoneMm >= 35 && t.KillZoneMm <= 40) s.Count35_40++;
                else if (t.KillZoneMm >= 30 && t.KillZoneMm <= 34) s.Count30_34++;
                else if (t.KillZoneMm >= 25 && t.KillZoneMm <= 29) s.Count25_29++;
                else if (t.KillZoneMm >= 20 && t.KillZoneMm <= 24) s.Count20_24++;
                else if (t.KillZoneMm >= 15 && t.KillZoneMm <= 19) s.Count15_19++;

                if (t.KillZoneMm >= 25 && t.KillZoneMm <= 34) s.Count25to34++;
                if (t.KillZoneMm >= 15 && t.KillZoneMm <= 19) s.Count15to19++;
            }

            // Ratings + averages for non-empty rows
            var filled = targets.Where(t => t.KillZoneMm > 0 && t.DistanceMeters > 0).ToList();
            if (filled.Count > 0)
            {
                decimal totalDist = 0, totalDiff = 0;
                foreach (var t in filled)
                {
                    var rr = ComputeRow(t);
                    if (rr.Rating == "Exp") s.CountExp++;
                    else if (rr.Rating == "Mod") s.CountMod++;
                    else if (rr.Rating == "Easy") s.CountEasy++;
                    totalDist += t.DistanceMeters;
                    totalDiff += rr.DiffScore;
                }
                s.AverageDistance = Math.Round(totalDist / filled.Count, 2);
                s.AverageDifficulty = Math.Round(totalDiff / filled.Count, 2);
            }

            // Overall validation
            if (s.StandCount != s.RequiredStanding)
                s.Errors.Add($"Standing count is {s.StandCount}, required {s.RequiredStanding}");
            if (s.KneelCount != s.RequiredKneeling)
                s.Errors.Add($"Kneeling count is {s.KneelCount}, required {s.RequiredKneeling}");
            if (s.Count25to34 < s.Required25to34Min)
                s.Errors.Add($"25–34mm targets = {s.Count25to34}, minimum {s.Required25to34Min}");
            if (s.Count15to19 < s.Required15to19Min)
                s.Errors.Add($"15–19mm targets = {s.Count15to19}, minimum {s.Required15to19Min}");

            return s;
        }

        private static decimal ComputeDifficulty(decimal distM, int killzoneMm, decimal diffFactor)
        {
            // a(x) = (x * 1.0936) * (1 / (killzone * 0.03937)) * diffFactor
            double j = killzoneMm * 0.03937;
            double L = (double)diffFactor;
            double A(double dist) => (dist * 1.0936) * (1.0 / j) * L;

            double k = (double)distM;
            double n;
            if (k <= 15)
                n = 0.05 * Math.Pow(A(k) - A(15), 2) + A(15) * 0.1 + A(22) - A(22) * 0.1;
            else if (k <= 22)
                n = A(15) * 0.1 + A(22) - A(22) * 0.1;
            else
                n = A(k);

            return (decimal)n;
        }

        private static (int fromMm, int toMm, int minM, int maxM)? LookupKillzoneRange(int kz)
        {
            foreach (var r in KillzoneRange)
                if (kz >= r.fromMm && kz <= r.toMm) return r;
            return null;
        }
    }
}

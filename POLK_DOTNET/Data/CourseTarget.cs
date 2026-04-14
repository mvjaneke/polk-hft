using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POLK_DOTNET.Data
{
    public class CourseTarget
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public int TargetNumber { get; set; }
        public int Lane { get; set; }

        // "UnStand" | "UnKneel" | "SupStand" | "SupKneel" | null (not yet set)
        [StringLength(20)]
        public string? Posture { get; set; }

        public bool IsInclineDecline { get; set; }
        public bool IsShaded { get; set; }

        public int KillZoneMm { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal DistanceMeters { get; set; }
    }
}

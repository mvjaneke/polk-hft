using System.ComponentModel.DataAnnotations;

namespace POLK_DOTNET.Data
{
    public class EventParticipant
    {
        public int Id { get; set; }

        public int EventRegistrationId { get; set; }
        public EventRegistration EventRegistration { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;
    }
}

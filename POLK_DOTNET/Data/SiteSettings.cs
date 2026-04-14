using System.ComponentModel.DataAnnotations;

namespace POLK_DOTNET.Data
{
    public class SiteSettings
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Key { get; set; } = null!;

        [StringLength(500)]
        public string Value { get; set; } = string.Empty;
    }
}

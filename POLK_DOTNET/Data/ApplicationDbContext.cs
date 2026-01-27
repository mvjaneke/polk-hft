using Microsoft.EntityFrameworkCore;

namespace POLK_DOTNET.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Event> Events { get; set; }
        public DbSet<GalleryImage> GalleryImages { get; set; }
        public DbSet<MembershipOption> MembershipOptions { get; set; }
    }

    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Time { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public int? Participants { get; set; }
        public int? MaxParticipants { get; set; }
    }

    public class GalleryImage
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
    }

    public class MembershipOption
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Price { get; set; }
        public string Features { get; set; } // Newline-separated features
        public int DisplayOrder { get; set; }
    }

}

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
        public DbSet<MembershipApplication> MembershipApplications { get; set; }
        public DbSet<Member> Members { get; set; }
    }

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
    }

    public class GalleryImage
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Category { get; set; } = null!;
    }

    public class MembershipOption
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Price { get; set; } = null!;
        public string Features { get; set; } = null!; // Newline-separated features
        public int DisplayOrder { get; set; }
    }

}

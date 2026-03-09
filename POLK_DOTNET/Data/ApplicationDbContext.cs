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
        public DbSet<CommitteeMember> CommitteeMembers { get; set; }
        public DbSet<Constitution> Constitutions { get; set; }
        public DbSet<EventRegistration> EventRegistrations { get; set; }
        public DbSet<EventParticipant> EventParticipants { get; set; }
        public DbSet<SiteSettings> SiteSettings { get; set; }
        public DbSet<GalleryAlbum> GalleryAlbums { get; set; }
    }

    public class GalleryImage
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Category { get; set; } = null!;
        public int GalleryAlbumId { get; set; }
        public GalleryAlbum GalleryAlbum { get; set; } = null!;
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

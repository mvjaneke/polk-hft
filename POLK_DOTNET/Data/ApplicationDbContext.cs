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
        public DbSet<CourseTarget> CourseTargets { get; set; }
        public DbSet<ShootingYear> ShootingYears { get; set; }
        public DbSet<ClubMember> ClubMembers { get; set; }
        public DbSet<MemberYear> MemberYears { get; set; }
        public DbSet<MembershipReminderLog> MembershipReminderLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ClubMember>().HasIndex(m => m.IdNumber);

            // A member is paid up for a year once; a second payment for the same year is a
            // duplicate to sort out, not a second membership.
            modelBuilder.Entity<MemberYear>()
                .HasIndex(y => new { y.ClubMemberId, y.ShootingYearId })
                .IsUnique();

            modelBuilder.Entity<MemberYear>()
                .HasOne(y => y.MembershipApplication)
                .WithMany()
                .HasForeignKey(y => y.MembershipApplicationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MemberYear>()
                .HasOne(y => y.ShootingYear)
                .WithMany()
                .HasForeignKey(y => y.ShootingYearId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MembershipApplication>()
                .HasOne(a => a.ShootingYear)
                .WithMany()
                .HasForeignKey(a => a.ShootingYearId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Member>()
                .HasOne(m => m.ClubMember)
                .WithMany()
                .HasForeignKey(m => m.ClubMemberId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MembershipReminderLog>()
                .HasOne(r => r.ShootingYear)
                .WithMany()
                .HasForeignKey(r => r.ShootingYearId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MembershipReminderLog>()
                .HasIndex(r => new { r.ClubMemberId, r.ShootingYearId });

            modelBuilder.Entity<MembershipApplication>().HasIndex(a => a.AccessToken);
        }
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

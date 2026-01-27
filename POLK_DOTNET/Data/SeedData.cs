using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<ApplicationDbContext>>()))
            {
                context.Database.EnsureCreated();
                // Look for any events.
                if (!context.Events.Any())
                {
                    context.Events.AddRange(
                        new Event
                        {
                            Title = "Monthly HFT Competition",
                            StartDate = DateTime.Now.AddDays(10),
                            Time = "08:00 - 17:00",
                            Type = "competition",
                            Description = "Open to all members. Standard HFT rules apply.",
                            Location = "Main Course",
                            Participants = 24,
                            MaxParticipants = 30
                        },
                        new Event
                        {
                            Title = "Beginners Training Session",
                            StartDate = DateTime.Now.AddDays(15),
                            Time = "14:00 - 16:00",
                            Type = "training",
                            Description = "Perfect for new members. Equipment provided.",
                            Location = "Training Range",
                            Participants = 8,
                            MaxParticipants = 12
                        },
                        new Event
                        {
                            Title = "Winter League Round 1",
                            StartDate = DateTime.Now.AddDays(22),
                            Time = "09:00 - 13:00",
                            Type = "competition",
                            Description = "First round of our winter league series.",
                            Location = "Main Course",
                            Participants = 18,
                            MaxParticipants = 25
                        },
                        new Event
                        {
                            Title = "Family Fun Day",
                            StartDate = DateTime.Now.AddDays(30),
                            Time = "10:00 - 15:00",
                            Type = "social",
                            Description = "Bring your family for a fun day of shooting activities.",
                            Location = "All Ranges",
                            Participants = 45,
                            MaxParticipants = 60
                        },
                        new Event
                        {
                            Title = "Advanced Techniques Workshop",
                            StartDate = DateTime.Now.AddDays(7),
                            Time = "14:00 - 17:00",
                            Type = "training",
                            Description = "Learn advanced shooting positions and techniques.",
                            Location = "Training Range",
                            Participants = 6,
                            MaxParticipants = 8
                        },
                        new Event
                        {
                            Title = "Club Championship (Multi-Day)",
                            StartDate = DateTime.Now.AddDays(45),
                            EndDate = DateTime.Now.AddDays(46),
                            Time = "08:00 - 18:00",
                            Type = "competition",
                            Description = "Annual club championship with prizes for all categories. This is a 2-day event.",
                            Location = "Main Course",
                            Participants = 32,
                            MaxParticipants = 40
                        },
                        // Add some past events for historical data
                        new Event
                        {
                            Title = "Past Competition",
                            StartDate = DateTime.Now.AddDays(-30),
                            Time = "08:00 - 17:00",
                            Type = "competition",
                            Description = "A competition from last month.",
                            Location = "Main Course",
                            Participants = 20,
                            MaxParticipants = 30
                        }
                    );
                }

                if (!context.GalleryImages.Any())
                {
                    context.GalleryImages.AddRange(
                        new GalleryImage
                        {
                            FileName = "/gallery-shooter-1.jpg",
                            Title = "Precision Shooting",
                            Description = "Member demonstrating perfect prone position technique during monthly competition",
                            Category = "competition"
                        },
                        new GalleryImage
                        {
                            FileName = "/gallery-shooter-2.jpg",
                            Title = "Steady Aim",
                            Description = "Training session focusing on kneeling position stability and accuracy",
                            Category = "training"
                        },
                        new GalleryImage
                        {
                            FileName = "/gallery-targets.jpg",
                            Title = "Target Setup",
                            Description = "Professional target placement at various distances up to 42 meters",
                            Category = "competition"
                        },
                        new GalleryImage
                        {
                            FileName = "/gallery-club.jpg",
                            Title = "Club Community",
                            Description = "Members enjoying fellowship and sharing experiences at our monthly gathering",
                            Category = "social"
                        },
                        new GalleryImage
                        {
                            FileName = "/gallery-awards.jpg",
                            Title = "Champions Celebrated",
                            Description = "Annual championship awards ceremony recognizing outstanding achievements",
                            Category = "awards"
                        },
                        new GalleryImage
                        {
                            FileName = "/gallery-training.jpg",
                            Title = "Expert Coaching",
                            Description = "Personalized coaching sessions to improve shooting techniques",
                            Category = "training"
                        },
                        new GalleryImage
                        {
                            FileName = "/hft-competition.jpg",
                            Title = "Natural Course",
                            Description = "Competition underway on our beautiful woodland course",
                            Category = "competition"
                        },
                        new GalleryImage
                        {
                            FileName = "/hft-course.jpg",
                            Title = "Our Range",
                            Description = "Overview of our main HFT course with natural terrain features",
                            Category = "competition"
                        }
                    );
                }

                if (!context.MembershipOptions.Any())
                {
                    context.MembershipOptions.AddRange(
                        new MembershipOption
                        {
                            Title = "Basic",
                            Price = "R150/month",
                            Features = "Access to practice range\nBasic equipment\nMonthly coaching",
                            DisplayOrder = 1
                        },
                        new MembershipOption
                        {
                            Title = "Premium",
                            Price = "R250/month",
                            Features = "Full range access\nAdvanced equipment\nWeekly coaching\nCompetition entry",
                            DisplayOrder = 2
                        },
                        new MembershipOption
                        {
                            Title = "Elite",
                            Price = "R400/month",
                            Features = "VIP access\nPersonal coaching\nEquipment maintenance\nPriority booking",
                            DisplayOrder = 3
                        }
                    );
                }
                
                context.SaveChanges();
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace POLK_DOTNET.Pages
{
    public class AdminModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;

        private readonly ILogger<AdminModel> _logger; // Keep logger for other potential uses.

        public AdminModel(ApplicationDbContext context, IWebHostEnvironment hostingEnvironment, IConfiguration configuration, ILogger<AdminModel> logger)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public bool IsAuthenticated { get; set; }

        public IList<Event> Events { get; set; } = null!;
        public IList<GalleryImage> GalleryImages { get; set; } = null!;
        public IList<GalleryAlbum> GalleryAlbums { get; set; } = null!;
        public GalleryAlbum? SelectedAlbum { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? SelectedAlbumId { get; set; }
        public IList<MembershipOption> MembershipOptions { get; set; } = null!;
        public IList<MembershipApplication> MembershipApplications { get; set; } = null!;
        public IList<CommitteeMember> CommitteeMembers { get; set; } = null!; // New property
        public Constitution? CurrentConstitution { get; set; } // New property for Constitution management
        public IList<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
        [BindProperty(SupportsGet = true)]
        public int? SelectedEventId { get; set; }
        [BindProperty(SupportsGet = true)]
        public string ActiveTab { get; set; } = "gallery";

        public IList<EventStatsViewModel> EventStatistics { get; set; } = new List<EventStatsViewModel>();

        // Settings properties
        public string? SettingsMessage { get; set; }
        public string YocoPublicKey { get; set; } = string.Empty;
        public string YocoSecretKey { get; set; } = string.Empty;
        public string SmtpHost { get; set; } = string.Empty;
        public string SmtpPort { get; set; } = string.Empty;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string EmailFromAddress { get; set; } = string.Empty;
        public string EmailFromName { get; set; } = string.Empty;
        public bool EmailEnableSsl { get; set; } = true;
        public string SahftaApiBaseUrl { get; set; } = string.Empty;

        public class EventStatsViewModel
        {
            public int EventId { get; set; }
            public string EventTitle { get; set; }
            public DateTime EventStartDate { get; set; }
            public int TotalSignedUp { get; set; }
            public int TotalPaid { get; set; }
            public int TotalNotPaid { get; set; }
        }

        public async Task OnGetAsync(string password)
        {
            if (password == _configuration["AdminPassword"])
            {
                HttpContext.Session.SetString("IsAuthenticated", "true");
            }

            if (HttpContext.Session.GetString("IsAuthenticated") == "true")
            {
                IsAuthenticated = true;
                Events = await _context.Events
                    .OrderBy(e => e.StartDate)
                    .ToListAsync();
                GalleryImages = await _context.GalleryImages.ToListAsync();
                GalleryAlbums = await _context.GalleryAlbums
                    .Include(a => a.Images)
                    .OrderByDescending(a => a.EventDate)
                    .ToListAsync();

                if (SelectedAlbumId.HasValue)
                {
                    SelectedAlbum = GalleryAlbums.FirstOrDefault(a => a.Id == SelectedAlbumId.Value);
                }
                MembershipOptions = await _context.MembershipOptions.OrderBy(m => m.DisplayOrder).ToListAsync();
                MembershipApplications = await _context.MembershipApplications
                                                .Include(ma => ma.Members)
                                                .OrderByDescending(ma => ma.SubmittedDate)
                                                .ToListAsync();
                CommitteeMembers = await _context.CommitteeMembers.OrderBy(cm => cm.Order).ToListAsync(); // Fetch committee members
                CurrentConstitution = await _context.Constitutions.FirstOrDefaultAsync(); // Fetch the single Constitution entry

                // Fetch event statistics
                EventStatistics = await _context.Events
                    .Where(e => e.IsClubEvent) // Only for club events
                    .Select(e => new EventStatsViewModel
                    {
                        EventId = e.Id,
                        EventTitle = e.Title,
                        EventStartDate = e.StartDate,
                        TotalSignedUp = _context.EventRegistrations.Count(er => er.EventId == e.Id),
                        TotalPaid = _context.EventRegistrations.Count(er => er.EventId == e.Id && er.Status == "Paid"),
                        TotalNotPaid = _context.EventRegistrations.Count(er => er.EventId == e.Id && er.Status != "Paid")
                    })
                    .OrderByDescending(es => es.EventStartDate)
                    .ToListAsync();

                // Load site settings
                var settings = await _context.SiteSettings.ToListAsync();
                YocoPublicKey = settings.FirstOrDefault(s => s.Key == "Yoco:PublicKey")?.Value ?? string.Empty;
                YocoSecretKey = settings.FirstOrDefault(s => s.Key == "Yoco:SecretKey")?.Value ?? string.Empty;
                SmtpHost = settings.FirstOrDefault(s => s.Key == "Email:SmtpHost")?.Value ?? string.Empty;
                SmtpPort = settings.FirstOrDefault(s => s.Key == "Email:SmtpPort")?.Value ?? string.Empty;
                SmtpUsername = settings.FirstOrDefault(s => s.Key == "Email:SmtpUsername")?.Value ?? string.Empty;
                SmtpPassword = settings.FirstOrDefault(s => s.Key == "Email:SmtpPassword")?.Value ?? string.Empty;
                EmailFromAddress = settings.FirstOrDefault(s => s.Key == "Email:FromAddress")?.Value ?? string.Empty;
                EmailFromName = settings.FirstOrDefault(s => s.Key == "Email:FromName")?.Value ?? string.Empty;
                EmailEnableSsl = settings.FirstOrDefault(s => s.Key == "Email:EnableSsl")?.Value?.ToLower() != "false";
                SahftaApiBaseUrl = settings.FirstOrDefault(s => s.Key == "Sahfta:ApiBaseUrl")?.Value ?? string.Empty;

                if (SelectedEventId.HasValue)
                {
                    EventRegistrations = await _context.EventRegistrations
                        .Where(er => er.EventId == SelectedEventId.Value)
                        .OrderByDescending(er => er.RegistrationDate)
                        .ToListAsync();
                }
            }
        }

        public async Task<IActionResult> OnPostMarkAsPaidAsync(int registrationId, int? eventId)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var registration = await _context.EventRegistrations.FindAsync(registrationId);
            if (registration != null)
            {
                registration.Status = "Paid";
                await _context.SaveChangesAsync();
            }

            // Redirect back to the same view with the eventId selected
            return RedirectToPage(new { SelectedEventId = eventId });
        }

        // Removed [BindProperty] for CommitteeMember
        // public CommitteeMember CommitteeMember { get; set; } = default!; 

        public async Task<IActionResult> OnPostAsync(string password)
        {
            if (password == _configuration["AdminPassword"])
            {
                HttpContext.Session.SetString("IsAuthenticated", "true");
                return RedirectToPage();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddCommitteeMemberAsync(string name, string position, int order)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var newCommitteeMember = new CommitteeMember
            {
                Name = name,
                Position = position,
                Order = order
            };

            _context.CommitteeMembers.Add(newCommitteeMember);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteCommitteeMemberAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var memberToDelete = await _context.CommitteeMembers.FindAsync(id);

            if (memberToDelete != null)
            {
                _context.CommitteeMembers.Remove(memberToDelete);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddEventAsync(string title, DateTime startDate, DateTime? endDate, string time, string type, string description, string location, int? participants, int? maxParticipants, string color, bool isClubEvent)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var newEvent = new Event
            {
                Title = title,
                StartDate = startDate,
                EndDate = endDate,
                Time = time,
                Type = type,
                Description = description,
                Location = location,
                Participants = participants,
                MaxParticipants = maxParticipants,
                Color = color,
                IsClubEvent = isClubEvent // Assign new property
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteEventAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var eventToDelete = await _context.Events.FindAsync(id);

            if (eventToDelete != null)
            {
                _context.Events.Remove(eventToDelete);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateAlbumAsync(string title, string? description, string category, DateTime eventDate)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            var album = new GalleryAlbum
            {
                Title = title,
                Description = description,
                Category = category,
                EventDate = eventDate
            };

            _context.GalleryAlbums.Add(album);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { ActiveTab = "gallery" });
        }

        public async Task<IActionResult> OnPostEditAlbumAsync(int id, string title, string? description, string category, DateTime eventDate)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            var album = await _context.GalleryAlbums.FindAsync(id);
            if (album != null)
            {
                album.Title = title;
                album.Description = description;
                album.Category = category;
                album.EventDate = eventDate;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { ActiveTab = "gallery", SelectedAlbumId = id });
        }

        public async Task<IActionResult> OnPostDeleteAlbumAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            var album = await _context.GalleryAlbums.Include(a => a.Images).FirstOrDefaultAsync(a => a.Id == id);
            if (album != null)
            {
                // Delete all image files from disk
                foreach (var image in album.Images)
                {
                    DeleteImageFile(image.FileName);
                }

                // Delete cover image if it's not one of the album images
                if (!string.IsNullOrEmpty(album.CoverImageFileName))
                {
                    DeleteImageFile(album.CoverImageFileName);
                }

                _context.GalleryAlbums.Remove(album);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { ActiveTab = "gallery" });
        }

        public async Task<IActionResult> OnPostUploadImagesAsync(int albumId, List<IFormFile> images)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            var album = await _context.GalleryAlbums.FindAsync(albumId);
            if (album == null)
                return RedirectToPage(new { ActiveTab = "gallery" });

            var uploadFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "wwwroot", "img");
            Directory.CreateDirectory(uploadFolder);

            foreach (var image in images)
            {
                if (image.Length > 0)
                {
                    // Generate unique filename to avoid collisions
                    var extension = Path.GetExtension(image.FileName);
                    var uniqueName = $"{Guid.NewGuid()}{extension}";
                    var imagePath = Path.Combine(uploadFolder, uniqueName);

                    using (var stream = new FileStream(imagePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    var galleryImage = new GalleryImage
                    {
                        FileName = $"/img/{uniqueName}",
                        Title = Path.GetFileNameWithoutExtension(image.FileName),
                        Description = "",
                        Category = album.Category,
                        GalleryAlbumId = albumId
                    };

                    _context.GalleryImages.Add(galleryImage);

                    // Set cover image to first uploaded image if album has no cover
                    if (string.IsNullOrEmpty(album.CoverImageFileName))
                    {
                        album.CoverImageFileName = $"/img/{uniqueName}";
                    }
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToPage(new { ActiveTab = "gallery", SelectedAlbumId = albumId });
        }

        public async Task<IActionResult> OnPostDeleteImageAsync(int id, int? albumId)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            var imageToDelete = await _context.GalleryImages.FindAsync(id);

            if (imageToDelete != null)
            {
                DeleteImageFile(imageToDelete.FileName);
                _context.GalleryImages.Remove(imageToDelete);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { ActiveTab = "gallery", SelectedAlbumId = albumId });
        }

        public async Task<IActionResult> OnPostSetCoverImageAsync(int imageId, int albumId)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            var image = await _context.GalleryImages.FindAsync(imageId);
            var album = await _context.GalleryAlbums.FindAsync(albumId);

            if (image != null && album != null)
            {
                album.CoverImageFileName = image.FileName;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { ActiveTab = "gallery", SelectedAlbumId = albumId });
        }

        private void DeleteImageFile(string fileName)
        {
            var relativeImagePath = fileName.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_hostingEnvironment.ContentRootPath, "wwwroot", relativeImagePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        public async Task<IActionResult> OnPostAddMembershipOptionAsync(string title, string price, string features, int displayOrder)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var newOption = new MembershipOption
            {
                Title = title,
                Price = price,
                Features = features,
                DisplayOrder = displayOrder
            };

            _context.MembershipOptions.Add(newOption);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMembershipOptionAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var optionToDelete = await _context.MembershipOptions.FindAsync(id);

            if (optionToDelete != null)
            {
                _context.MembershipOptions.Remove(optionToDelete);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostApproveApplicationAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var application = await _context.MembershipApplications.FindAsync(id);
            if (application != null)
            {
                application.Status = "Approved";
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectApplicationAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var application = await _context.MembershipApplications.FindAsync(id);
            if (application != null)
            {
                application.Status = "Rejected";
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSaveConstitutionAsync(string content)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var constitution = await _context.Constitutions.FirstOrDefaultAsync();

            if (constitution == null)
            {
                constitution = new Constitution { Content = content };
                _context.Constitutions.Add(constitution);
            }
            else
            {
                constitution.Content = content;
                _context.Constitutions.Update(constitution); // Or simply change state: _context.Entry(constitution).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSaveYocoSettingsAsync(string yocoPublicKey, string yocoSecretKey)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            await SaveSettingAsync("Yoco:PublicKey", yocoPublicKey ?? string.Empty);
            await SaveSettingAsync("Yoco:SecretKey", yocoSecretKey ?? string.Empty);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { ActiveTab = "settings" });
        }

        public async Task<IActionResult> OnPostSaveSahftaSettingsAsync(string sahftaApiBaseUrl)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToPage();

            await SaveSettingAsync("Sahfta:ApiBaseUrl", (sahftaApiBaseUrl ?? string.Empty).TrimEnd('/'));
            await _context.SaveChangesAsync();

            return RedirectToPage(new { ActiveTab = "settings" });
        }

        public async Task<IActionResult> OnPostSaveEmailSettingsAsync(string smtpHost, string smtpPort, string smtpUsername, string smtpPassword, string fromAddress, string fromName, bool enableSsl)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            await SaveSettingAsync("Email:SmtpHost", smtpHost ?? string.Empty);
            await SaveSettingAsync("Email:SmtpPort", smtpPort ?? string.Empty);
            await SaveSettingAsync("Email:SmtpUsername", smtpUsername ?? string.Empty);
            await SaveSettingAsync("Email:SmtpPassword", smtpPassword ?? string.Empty);
            await SaveSettingAsync("Email:FromAddress", fromAddress ?? string.Empty);
            await SaveSettingAsync("Email:FromName", fromName ?? string.Empty);
            await SaveSettingAsync("Email:EnableSsl", enableSsl ? "true" : "false");
            await _context.SaveChangesAsync();

            return RedirectToPage(new { ActiveTab = "settings" });
        }

        private async Task SaveSettingAsync(string key, string value)
        {
            var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                setting = new SiteSettings { Key = key, Value = value };
                _context.SiteSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
            }
        }
    }
}
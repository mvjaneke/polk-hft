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

        public AdminModel(ApplicationDbContext context, IWebHostEnvironment hostingEnvironment, IConfiguration configuration)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
        }

        [BindProperty]
        public bool IsAuthenticated { get; set; }

        public IList<Event> Events { get; set; } = null!;
        public IList<GalleryImage> GalleryImages { get; set; } = null!;
        public IList<MembershipOption> MembershipOptions { get; set; } = null!;
        public IList<MembershipApplication> MembershipApplications { get; set; } = null!;
        public IList<CommitteeMember> CommitteeMembers { get; set; } = null!; // New property

        public async Task OnGetAsync(string password)
        {
            if (password == _configuration["AdminPassword"])
            {
                HttpContext.Session.SetString("IsAuthenticated", "true");
            }

            if (HttpContext.Session.GetString("IsAuthenticated") == "true")
            {
                IsAuthenticated = true;
                Events = await _context.Events.OrderBy(e => e.StartDate).ToListAsync();
                GalleryImages = await _context.GalleryImages.ToListAsync();
                MembershipOptions = await _context.MembershipOptions.OrderBy(m => m.DisplayOrder).ToListAsync();
                MembershipApplications = await _context.MembershipApplications
                                                .Include(ma => ma.Members)
                                                .OrderByDescending(ma => ma.SubmittedDate)
                                                .ToListAsync();
                CommitteeMembers = await _context.CommitteeMembers.OrderBy(cm => cm.Order).ToListAsync(); // Fetch committee members
            }
        }

        [BindProperty]
        public CommitteeMember CommitteeMember { get; set; } = default!;

        public async Task<IActionResult> OnPostAsync(string password)
        {
            if (password == _configuration["AdminPassword"])
            {
                HttpContext.Session.SetString("IsAuthenticated", "true");
                return RedirectToPage();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddCommitteeMemberAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            if (!ModelState.IsValid)
            {
                // Reload data needed for the page if validation fails
                await OnGetAsync(null); // Pass null as password for OnGetAsync when refreshing page.
                return Page();
            }

            _context.CommitteeMembers.Add(CommitteeMember);
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

        public async Task<IActionResult> OnPostAddEventAsync(string title, DateTime startDate, DateTime? endDate, string time, string type, string description, string location, int? participants, int? maxParticipants, string color)
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
                Color = color
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

        public async Task<IActionResult> OnPostAddImageAsync(IFormFile image, string title, string description, string category)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            if (image != null)
            {
                var uploadFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "wwwroot", "img");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }
                var imagePath = Path.Combine(uploadFolder, image.FileName);
                using (var stream = new FileStream(imagePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var galleryImage = new GalleryImage
                {
                    FileName = $"/img/{image.FileName}",
                    Title = title,
                    Description = description,
                    Category = category
                };

                _context.GalleryImages.Add(galleryImage);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteImageAsync(int id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage();
            }

            var imageToDelete = await _context.GalleryImages.FindAsync(id);

            if (imageToDelete != null)
            {
                // Remove leading slash and normalize directory separators for correct path combination
                var relativeImagePath = imageToDelete.FileName.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPathToDelete = Path.Combine(_hostingEnvironment.ContentRootPath, "wwwroot", relativeImagePath);
                
                if (System.IO.File.Exists(fullPathToDelete))
                {
                    System.IO.File.Delete(fullPathToDelete);
                }

                _context.GalleryImages.Remove(imageToDelete);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
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
    }
}

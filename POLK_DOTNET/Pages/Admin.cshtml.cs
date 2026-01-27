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

        public IList<Event> Events { get; set; }
        public IList<GalleryImage> GalleryImages { get; set; }
        public IList<MembershipOption> MembershipOptions { get; set; }

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
            }
        }

        public async Task<IActionResult> OnPostAsync(string password)
        {
            if (password == _configuration["AdminPassword"])
            {
                HttpContext.Session.SetString("IsAuthenticated", "true");
                return RedirectToPage();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddEventAsync(string title, DateTime startDate, DateTime? endDate, string time, string type, string description, string location, int? participants, int? maxParticipants)
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
                MaxParticipants = maxParticipants
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
                var imagePath = Path.Combine(_hostingEnvironment.WebRootPath, "img", image.FileName);
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
                // Remove leading slash to correctly combine with WebRootPath
                var fileName = imageToDelete.FileName.TrimStart('/');
                var imagePath = Path.Combine(_hostingEnvironment.WebRootPath, fileName);
                
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
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
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace POLK_DOTNET.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GalleryImagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GalleryImagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/GalleryImages
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GalleryImage>>> GetGalleryImages()
        {
            return await _context.GalleryImages.ToListAsync();
        }
    }
}

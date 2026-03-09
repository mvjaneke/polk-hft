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

    [Route("api/[controller]")]
    [ApiController]
    public class GalleryAlbumsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GalleryAlbumsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/GalleryAlbums
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetGalleryAlbums()
        {
            var albums = await _context.GalleryAlbums
                .OrderByDescending(a => a.EventDate)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.Category,
                    a.CoverImageFileName,
                    a.EventDate,
                    ImageCount = a.Images.Count
                })
                .ToListAsync();

            return Ok(albums);
        }

        // GET: api/GalleryAlbums/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetGalleryAlbum(int id)
        {
            var album = await _context.GalleryAlbums
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.Category,
                    a.CoverImageFileName,
                    a.EventDate,
                    Images = a.Images.Select(i => new
                    {
                        i.Id,
                        i.FileName,
                        i.Title,
                        i.Description,
                        i.Category
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (album == null)
                return NotFound();

            return Ok(album);
        }
    }
}

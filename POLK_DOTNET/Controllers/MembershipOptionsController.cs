using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POLK_DOTNET.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembershipOptionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MembershipOptionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/MembershipOptions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MembershipOption>>> GetMembershipOptions()
        {
            return await _context.MembershipOptions.OrderBy(m => m.DisplayOrder).ToListAsync();
        }
    }
}

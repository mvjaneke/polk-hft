using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Linq;
using System.Threading.Tasks;

namespace POLK_DOTNET.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConstitutionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ConstitutionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Constitution
        [HttpGet]
        public async Task<ActionResult<Constitution>> GetConstitution()
        {
            var constitution = await _context.Constitutions.FirstOrDefaultAsync();

            if (constitution == null)
            {
                // If no constitution is found, return a default one or an empty one.
                return new Constitution { Id = 0, Content = "<p>No constitution has been uploaded yet.</p>" };
            }

            return constitution;
        }

        // POST: api/Constitution
        [HttpPost]
        public async Task<ActionResult<Constitution>> PostConstitution(Constitution constitution)
        {
            var existingConstitution = await _context.Constitutions.FirstOrDefaultAsync();
            if (existingConstitution != null)
            {
                existingConstitution.Content = constitution.Content;
                _context.Entry(existingConstitution).State = EntityState.Modified;
            }
            else
            {
                _context.Constitutions.Add(constitution);
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetConstitution), new { id = constitution.Id }, constitution);
        }
    }
}

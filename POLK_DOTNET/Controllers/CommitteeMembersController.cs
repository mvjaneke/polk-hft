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
    public class CommitteeMembersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CommitteeMembersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/CommitteeMembers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommitteeMember>>> GetCommitteeMembers()
        {
            return await _context.CommitteeMembers.OrderBy(cm => cm.Order).ToListAsync();
        }

        // GET: api/CommitteeMembers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CommitteeMember>> GetCommitteeMember(int id)
        {
            var committeeMember = await _context.CommitteeMembers.FindAsync(id);

            if (committeeMember == null)
            {
                return NotFound();
            }

            return committeeMember;
        }

        // POST: api/CommitteeMembers
        [HttpPost]
        public async Task<ActionResult<CommitteeMember>> PostCommitteeMember(CommitteeMember committeeMember)
        {
            _context.CommitteeMembers.Add(committeeMember);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCommitteeMember), new { id = committeeMember.Id }, committeeMember);
        }

        // PUT: api/CommitteeMembers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCommitteeMember(int id, CommitteeMember committeeMember)
        {
            if (id != committeeMember.Id)
            {
                return BadRequest();
            }

            _context.Entry(committeeMember).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CommitteeMemberExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/CommitteeMembers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCommitteeMember(int id)
        {
            var committeeMember = await _context.CommitteeMembers.FindAsync(id);
            if (committeeMember == null)
            {
                return NotFound();
            }

            _context.CommitteeMembers.Remove(committeeMember);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CommitteeMemberExists(int id)
        {
            return _context.CommitteeMembers.Any(e => e.Id == id);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace POLK_DOTNET.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MembersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("check-existence")]
        public async Task<IActionResult> CheckExistence([FromQuery] string idNumber)
        {
            if (string.IsNullOrEmpty(idNumber))
            {
                return BadRequest("ID number is required.");
            }

            var memberExists = await _context.Members
                .AnyAsync(m => m.IdNumber == idNumber && m.MembershipApplication.Status == "Approved");

            var adminFeeOption = await _context.MembershipOptions
                .FirstOrDefaultAsync(mo => mo.Title.Contains("One Time Admin fee"));

            decimal parsedAdminFee = 0;
            if (adminFeeOption != null)
            {
                string priceString = adminFeeOption.Price.Replace("R", "").Replace("/month", "").Trim();
                decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedAdminFee);
            }

            return Ok(new { 
                memberExists,
                adminFee = memberExists ? 0 : parsedAdminFee
            });
        }
    }
}

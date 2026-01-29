using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using System.Linq;
using System.Threading.Tasks;

namespace POLK_DOTNET.Pages
{
    public class EditMembershipOptionModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditMembershipOptionModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public MembershipOption MembershipOption { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage("/Admin");
            }

            if (id == null)
            {
                return NotFound();
            }

            MembershipOption = await _context.MembershipOptions.FirstOrDefaultAsync(m => m.Id == id);

            if (MembershipOption == null)
            {
                return NotFound();
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToPage("/Admin");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(MembershipOption).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.MembershipOptions.Any(e => e.Id == MembershipOption.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("/Admin");
        }
    }
}

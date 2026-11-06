using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages
{
    /// <summary>
    /// The applicant's own view of their application, reached by an unguessable token (in the
    /// confirmation email too), so it can show bank details and a payment reference without
    /// letting anyone page through other people's applications by id.
    /// </summary>
    public class ApplicationSubmittedModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;

        public ApplicationSubmittedModel(ApplicationDbContext context, MembershipService membership)
        {
            _context = context;
            _membership = membership;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = "";

        /// <summary>success, pending, cancelled, failed or unavailable (card link could not be created).</summary>
        [BindProperty(SupportsGet = true)]
        public string? Payment { get; set; }

        public MembershipApplication Application { get; private set; } = null!;
        public PaymentDetails Bank { get; private set; } = new();
        public string Reference { get; private set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Token)) return NotFound();

            var app = await _context.MembershipApplications
                .Include(a => a.Members)
                .Include(a => a.ShootingYear)
                .FirstOrDefaultAsync(a => a.AccessToken == Token);
            if (app == null) return NotFound();

            Application = app;
            Bank = await _membership.GetPaymentDetailsAsync();
            Reference = MembershipService.PaymentReference(app);
            return Page();
        }
    }
}

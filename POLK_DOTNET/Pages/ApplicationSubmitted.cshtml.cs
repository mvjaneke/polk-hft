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
        private readonly MembershipCardPaymentService _cardPayment;
        private readonly ILogger<ApplicationSubmittedModel> _logger;

        public ApplicationSubmittedModel(ApplicationDbContext context, MembershipService membership, MembershipCardPaymentService cardPayment, ILogger<ApplicationSubmittedModel> logger)
        {
            _context = context;
            _membership = membership;
            _cardPayment = cardPayment;
            _logger = logger;
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
            var app = await LoadAsync();
            if (app == null) return NotFound();

            Application = app;
            Bank = await _membership.GetPaymentDetailsAsync();
            Reference = MembershipService.PaymentReference(app);
            return Page();
        }

        /// <summary>
        /// "Pay by card" from this page: a new payment link for the same application, so an
        /// applicant whose card payment failed doesn't have to fill in the form again.
        /// </summary>
        public async Task<IActionResult> OnPostPayByCardAsync()
        {
            var app = await LoadAsync();
            if (app == null) return NotFound();

            if (app.IsPaid || app.IsClosed || app.ApprovalStatus == ApprovalStatuses.Rejected || app.TotalAmount <= 0)
                return RedirectToPage(new { token = app.AccessToken });

            if (app.PaymentMethod != "Card")
            {
                app.PaymentMethod = "Card";
                await _context.SaveChangesAsync();
            }

            var payUrl = await _cardPayment.CreateAsync(app, $"{Request.Scheme}://{Request.Host}", retry: true);
            if (payUrl != null) return Redirect(payUrl);

            _logger.LogWarning("Membership application {Id}: no card payment link was returned on retry", app.Id);
            return RedirectToPage(new { token = app.AccessToken, payment = "unavailable" });
        }

        private Task<MembershipApplication?> LoadAsync() =>
            string.IsNullOrWhiteSpace(Token)
                ? Task.FromResult<MembershipApplication?>(null)
                : _context.MembershipApplications
                    .Include(a => a.Members)
                    .Include(a => a.ShootingYear)
                    .FirstOrDefaultAsync(a => a.AccessToken == Token);
    }
}

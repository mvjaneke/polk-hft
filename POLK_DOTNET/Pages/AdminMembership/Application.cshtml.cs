using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Pages.AdminMembership
{
    public class ApplicationModel : MembershipAdminPage
    {
        private readonly ApplicationDbContext _context;
        private readonly MembershipService _membership;
        private readonly ApplicationPdfService _pdf;

        public ApplicationModel(ApplicationDbContext context, MembershipService membership, ApplicationPdfService pdf)
        {
            _context = context;
            _membership = membership;
            _pdf = pdf;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public MembershipApplication Application { get; private set; } = null!;
        public string Reference { get; private set; } = "";
        public string Today => ClubClock.Today.ToString("yyyy-MM-dd");

        public async Task<IActionResult> OnGetAsync()
        {
            var app = await LoadAsync();
            if (app == null) return NotFound();
            Application = app;
            Reference = MembershipService.PaymentReference(app);
            return Page();
        }

        public async Task<IActionResult> OnGetPdfAsync()
        {
            var app = await LoadAsync();
            if (app == null) return NotFound();
            return File(_pdf.GenerateApplicationPdf(app), "application/pdf", $"MembershipApplication_{app.Id}.pdf");
        }

        public async Task<IActionResult> OnPostMarkPaidAsync(string? paidOn, string? amount, bool sendEmail)
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();

            if (!TryParseDate(paidOn, out var date))
            {
                ErrorMessage = "Enter the date the payment was received.";
                return RedirectToPage(new { Id });
            }
            if (!TryParseMoney(amount, out var paid))
            {
                ErrorMessage = "Enter the amount received, e.g. 700.00.";
                return RedirectToPage(new { Id });
            }

            var source = app.PaymentMethod == "Card" ? "Online" : "EFT";
            var done = await _membership.MarkPaidAsync(Id, source, date, paid, sendEmail);
            if (!done)
                Message = "This application was already marked paid.";
            else
            {
                Message = $"Marked paid. Everyone on application #{Id} is now on the member list as paid up for its year.";
                if (_membership.LastClosedDuplicates.Count > 0)
                    Message += $" Closed the unpaid duplicate{(_membership.LastClosedDuplicates.Count > 1 ? "s" : "")} {string.Join(", ", _membership.LastClosedDuplicates.Select(d => "#" + d))}.";
                if (sendEmail && _membership.LastConfirmationEmailSent) Message += " The applicant has been emailed.";
                else if (sendEmail) ErrorMessage = "The payment confirmation email to the applicant could not be sent. Check the email settings, or let them know yourself.";
            }
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostMarkUnpaidAsync()
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();

            app.PaymentStatus = PaymentStatuses.Unpaid;
            app.PaidDate = null;
            app.AmountPaid = null;
            await _membership.RemoveYearsFromApplicationAsync(Id);
            await _context.SaveChangesAsync();

            Message = "Marked unpaid. The paid year this application had recorded has been removed from the member list.";
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();
            app.ApprovalStatus = ApprovalStatuses.Approved;
            await _context.SaveChangesAsync();
            Message = "Application approved.";
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();
            app.ApprovalStatus = ApprovalStatuses.Rejected;
            if (app.IsPaid)
                await _membership.RemoveYearsFromApplicationAsync(Id);
            await _context.SaveChangesAsync();
            Message = app.IsPaid
                ? "Application rejected and its paid year removed from the member list. It was paid, so a refund may be due."
                : "Application rejected.";
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostCloseAsync(string? reason)
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();
            if (app.IsPaid)
            {
                ErrorMessage = "This application is paid, so it can't be closed. Reject it instead if it shouldn't count.";
                return RedirectToPage(new { Id });
            }

            var why = reason switch { "Duplicate" => "a duplicate", "Test" => "a test", _ => "abandoned" };
            app.ApprovalStatus = ApprovalStatuses.Closed;
            app.AddNote($"{ClubClock.Today:d MMM yyyy}: closed by admin as {why}.");
            await _context.SaveChangesAsync();
            Message = "Application closed. It no longer shows under Needs action or Unpaid.";
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostReopenAsync()
        {
            var app = await _context.MembershipApplications.Include(a => a.Members).FirstOrDefaultAsync(a => a.Id == Id);
            if (app == null) return NotFound();
            var wasClosed = app.IsClosed;
            app.ApprovalStatus = ApprovalStatuses.Pending;
            if (app.IsPaid)
                await _membership.RecordMembershipAsync(app, app.PaymentMethod == "Card" ? "Online" : "EFT");
            await _context.SaveChangesAsync();
            Message = (wasClosed ? "Application reopened." : "Application set back to pending.") + (app.IsPaid ? " Its paid year is back on the member list." : "");
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostFlagsCheckedAsync(bool reviewed)
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();
            app.FlagsReviewed = reviewed;
            await _context.SaveChangesAsync();
            Message = reviewed ? "Marked as checked." : "Marked as still to check.";
            return RedirectToPage(new { Id });
        }

        public async Task<IActionResult> OnPostNotesAsync(string? notes)
        {
            var app = await _context.MembershipApplications.FindAsync(Id);
            if (app == null) return NotFound();
            app.AdminNotes = MembershipService.NullIfEmpty(notes);
            await _context.SaveChangesAsync();
            Message = "Notes saved.";
            return RedirectToPage(new { Id });
        }

        private Task<MembershipApplication?> LoadAsync() =>
            _context.MembershipApplications
                .Include(a => a.Members).ThenInclude(m => m.ClubMember)
                .Include(a => a.ShootingYear)
                .FirstOrDefaultAsync(a => a.Id == Id);
    }
}

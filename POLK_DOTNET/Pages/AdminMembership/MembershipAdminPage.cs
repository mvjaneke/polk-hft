using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace POLK_DOTNET.Pages.AdminMembership
{
    /// <summary>
    /// Base for the membership admin pages. Every handler, GET or POST, is behind the same
    /// session login as the rest of the admin area, checked once here rather than in each one.
    /// </summary>
    public abstract class MembershipAdminPage : PageModel
    {
        [TempData]
        public string? Message { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                context.Result = new RedirectToPageResult("/Admin");
                return;
            }
            await next();

            // Handed to the shared nav partial, which shows the flash messages.
            ViewData["Message"] = Message;
            ViewData["ErrorMessage"] = ErrorMessage;
        }

        /// <summary>
        /// Reads an amount typed by the admin: "450", "450.00", "450,00", "R 450" or "1 050.00".
        /// The live server may run a culture where the decimal separator is a comma, so the
        /// built-in binder can't be trusted with money.
        /// </summary>
        public static bool TryParseMoney(string? text, out decimal amount)
        {
            amount = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var s = new string(text.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
            if (s.Count(c => c is '.' or ',') == 1) s = s.Replace(',', '.');
            else s = s.Replace(",", "");
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && amount >= 0;
        }

        public static string MoneyInput(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        public static bool TryParseDate(string? text, out DateTime date) =>
            DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}

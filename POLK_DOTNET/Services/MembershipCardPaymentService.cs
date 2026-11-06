using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    /// <summary>
    /// Creates the card payment link for a membership application, both straight after applying
    /// and when the applicant retries from their confirmation page after a failed or cancelled
    /// payment. Retrying reuses the same application, so no duplicate is created.
    /// </summary>
    public class MembershipCardPaymentService
    {
        private readonly MembershipService _membership;
        private readonly IkhokhaPaymentService _ikhokha;
        private readonly YocoCheckoutService _yoco;
        private readonly ILogger<MembershipCardPaymentService> _logger;

        public MembershipCardPaymentService(MembershipService membership, IkhokhaPaymentService ikhokha, YocoCheckoutService yoco, ILogger<MembershipCardPaymentService> logger)
        {
            _membership = membership;
            _ikhokha = ikhokha;
            _yoco = yoco;
            _logger = logger;
        }

        /// <summary>
        /// iKhokha reference for a payment attempt. The first attempt keeps the original
        /// "APP-{id}"; a retry gets its own suffix so the status check looks at that attempt's
        /// payment link and not the earlier failed one.
        /// </summary>
        public static string ExternalReference(int applicationId, string? attempt) =>
            string.IsNullOrEmpty(attempt) ? $"APP-{applicationId}" : $"APP-{applicationId}-{attempt}";

        /// <summary>Parses "APP-12" or "APP-12-261106163700" back to 12.</summary>
        public static bool TryParseExternalReference(string reference, out int applicationId)
        {
            applicationId = 0;
            if (!reference.StartsWith("APP-", StringComparison.OrdinalIgnoreCase)) return false;
            var rest = reference["APP-".Length..];
            var dash = rest.IndexOf('-');
            return int.TryParse(dash < 0 ? rest : rest[..dash], out applicationId);
        }

        /// <summary>Returns the gateway URL to send the applicant to, or null if none could be created.</summary>
        public async Task<string?> CreateAsync(MembershipApplication application, string baseUrl, bool retry = false)
        {
            var id = application.Id;
            var attempt = retry ? ClubClock.Now.ToString("yyMMddHHmmss") : null;
            var query = $"applicationId={id}&token={application.AccessToken}" + (attempt != null ? $"&attempt={attempt}" : "");
            var successUrl = $"{baseUrl}/payment-result?status=success&{query}";
            var cancelUrl = $"{baseUrl}/payment-result?status=cancelled&{query}";
            var failureUrl = $"{baseUrl}/payment-result?status=failed&{query}";
            var description = $"POLK Membership {application.ShootingYear?.Name} #{id}";

            try
            {
                var gateway = (await _membership.GetSettingAsync("PaymentGateway")) ?? "Ikhokha";
                if (string.Equals(gateway, "Ikhokha", StringComparison.OrdinalIgnoreCase))
                {
                    var link = await _ikhokha.CreatePaymentLinkAsync(
                        application.TotalAmount, description,
                        externalTransactionId: ExternalReference(id, attempt),
                        requesterUrl: baseUrl,
                        callbackUrl: $"{baseUrl}/api/ikhokha/callback",
                        successPageUrl: successUrl,
                        failurePageUrl: failureUrl,
                        cancelUrl: cancelUrl);
                    return string.IsNullOrEmpty(link?.PaylinkUrl) ? null : link!.PaylinkUrl;
                }

                var checkout = await _yoco.CreateCheckoutAsync(application.TotalAmount, description, successUrl, cancelUrl, failureUrl,
                    new Dictionary<string, string> { { "applicationId", id.ToString() } });
                return string.IsNullOrEmpty(checkout?.RedirectUrl) ? null : checkout!.RedirectUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Membership application {Id}: creating the card payment failed", id);
                return null;
            }
        }
    }
}

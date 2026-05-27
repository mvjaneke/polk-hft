using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    // Talks to iKhokha's iK Pay API. Surface roughly mirrors YocoCheckoutService so
    // the Apply/RegisterEvent pages can switch gateways at runtime by reading the
    // PaymentGateway site setting.
    //
    // Endpoint: POST https://api.ikhokha.com/public-api/v1/api/payment
    // Auth:     IK-APPID header + IK-SIGN header (HMAC-SHA256 over urlPath + jsonBody, keyed with App Secret)
    // Webhook:  POST to our callbackUrl with body { paylinkID, status, externalTransactionID, responseCode },
    //           IK-SIGN computed over (callback path + raw body).
    // Status:   GET .../api/getStatus/external?externalReference=<id> OR .../api/getStatus/{paylinkID}
    public class IkhokhaPaymentService
    {
        private const string ApiEndpoint = "https://api.ikhokha.com/public-api/v1/api/payment";
        private const string StatusEndpointByExternal = "https://api.ikhokha.com/public-api/v1/api/getStatus/external";
        private const string StatusEndpointByPaylink = "https://api.ikhokha.com/public-api/v1/api/getStatus/";

        // System.Text.Json's default escaper turns `&`, `<`, `>`, `'`, `+` into \uXXXX.
        // iKhokha's signature verifier appears to reject when the JSON we sign doesn't
        // match what their parser canonicalises to. Use the relaxed escaper so the
        // body bytes we hash equal what JSON.parse / re-serialize would produce.
        private static readonly JsonSerializerOptions IkhokhaJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IkhokhaPaymentService> _logger;

        public IkhokhaPaymentService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<IkhokhaPaymentService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private async Task<IkhokhaConfig?> GetConfigAsync()
        {
            var settings = await _context.SiteSettings
                .Where(s => s.Key == "Ikhokha:AppId" || s.Key == "Ikhokha:AppSecret")
                .ToListAsync();

            var appId = settings.FirstOrDefault(s => s.Key == "Ikhokha:AppId")?.Value?.Trim();
            var appSecret = settings.FirstOrDefault(s => s.Key == "Ikhokha:AppSecret")?.Value?.Trim();
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret)) return null;

            return new IkhokhaConfig { AppId = appId, AppSecret = appSecret };
        }

        public async Task<bool> IsConfiguredAsync() => (await GetConfigAsync()) != null;

        public async Task<IkhokhaPaymentLinkResponse?> CreatePaymentLinkAsync(
            decimal amountInRands,
            string description,
            string externalTransactionId,
            string requesterUrl,
            string callbackUrl,
            string successPageUrl,
            string failurePageUrl,
            string cancelUrl)
        {
            var config = await GetConfigAsync();
            if (config == null)
            {
                _logger.LogWarning("iKhokha CreatePaymentLink called but no AppId/AppSecret configured");
                return null;
            }

            // iKhokha rejects with HTTP 500 when URLs (especially callbackUrl) aren't
            // publicly reachable — they validate them server-side. Fail fast with a
            // clear error rather than send a doomed request from a dev machine.
            var nonPublic = new[] { callbackUrl, successPageUrl, failurePageUrl, cancelUrl, requesterUrl }
                .FirstOrDefault(u => IsNonPublicUrl(u));
            if (nonPublic != null)
            {
                throw new InvalidOperationException(
                    $"iKhokha requires publicly-reachable HTTPS URLs but got '{nonPublic}'. " +
                    "Run against your deployed environment, or expose your dev server with ngrok / Cloudflare Tunnel.");
            }

            // entityID is a per-request opaque reference. The IK-APPID header
            // identifies the merchant; sending the App ID here in addition to the
            // header produced HTTP 500 in testing.
            var body = new
            {
                entityID = Guid.NewGuid().ToString(),
                externalEntityID = "POLK",
                amount = (int)Math.Round(amountInRands * 100m), // cents
                currency = "ZAR",
                requesterUrl,
                description,
                paymentReference = Guid.NewGuid().ToString(),
                mode = "live",
                externalTransactionID = externalTransactionId,
                urls = new
                {
                    callbackUrl,
                    successPageUrl,
                    failurePageUrl,
                    cancelUrl
                }
            };

            var jsonBody = JsonSerializer.Serialize(body, IkhokhaJsonOptions);
            var signature = ComputeSignature(ApiEndpoint, jsonBody, config.AppSecret);

            var http = _httpClientFactory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("IK-APPID", config.AppId);
            req.Headers.Add("IK-SIGN", signature);

            _logger.LogInformation("iKhokha create-paylink — ext-txn {ExtTxn}, amount R{Amount:F2}, body length {Len}",
                externalTransactionId, amountInRands, jsonBody.Length);

            var response = await http.SendAsync(req);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("iKhokha create-paylink response — status {Status}, body: {Body}",
                (int)response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("iKhokha create-paylink HTTP failure — status {Status}, body {Body}", response.StatusCode, responseBody);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<IkhokhaPaymentLinkResponse>(responseBody);
            if (parsed == null)
            {
                _logger.LogError("iKhokha create-paylink — response could not be deserialised. Body: {Body}", responseBody);
                return null;
            }

            if (string.IsNullOrEmpty(parsed.PaylinkUrl))
            {
                _logger.LogError("iKhokha create-paylink rejected — responseCode {Code}, message '{Msg}'. Full body: {Body}",
                    parsed.ResponseCode, parsed.Message, responseBody);
            }
            else
            {
                _logger.LogInformation("iKhokha paylink created — paylinkID {PaylinkID}, url {Url}", parsed.PaylinkID, parsed.PaylinkUrl);
            }

            return parsed;
        }

        public async Task<IkhokhaStatusResponse?> GetStatusByExternalTxnAsync(string externalTransactionId)
        {
            var config = await GetConfigAsync();
            if (config == null) return null;

            var url = $"{StatusEndpointByExternal}?externalReference={Uri.EscapeDataString(externalTransactionId)}";
            return await GetStatusInternalAsync(url, config);
        }

        public async Task<IkhokhaStatusResponse?> GetStatusByPaylinkIdAsync(string paylinkId)
        {
            var config = await GetConfigAsync();
            if (config == null) return null;

            var url = $"{StatusEndpointByPaylink}{Uri.EscapeDataString(paylinkId)}";
            return await GetStatusInternalAsync(url, config);
        }

        private async Task<IkhokhaStatusResponse?> GetStatusInternalAsync(string url, IkhokhaConfig config)
        {
            var signature = ComputeSignature(url, string.Empty, config.AppSecret);

            var http = _httpClientFactory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("IK-APPID", config.AppId);
            req.Headers.Add("IK-SIGN", signature);

            var response = await http.SendAsync(req);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("iKhokha getStatus failed — status {Status}, body {Body}", response.StatusCode, responseBody);
                return null;
            }

            return JsonSerializer.Deserialize<IkhokhaStatusResponse>(responseBody);
        }

        public async Task<bool> VerifyWebhookSignatureAsync(string callbackUrlPath, string rawBody, string ikSignHeader)
        {
            if (string.IsNullOrWhiteSpace(ikSignHeader)) return false;
            var config = await GetConfigAsync();
            if (config == null) return false;

            var expected = ComputeSignature(callbackUrlPath, rawBody, config.AppSecret, alreadyPathOnly: true);
            return string.Equals(expected, ikSignHeader.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNonPublicUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return true;
            if (uri.Scheme != "https") return true;
            var host = uri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host == "::1") return true;
            if (host.StartsWith("10.") || host.StartsWith("192.168.")) return true;
            if (host.StartsWith("172."))
            {
                var parts = host.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var second) && second is >= 16 and <= 31) return true;
            }
            return false;
        }

        private static string ComputeSignature(string urlOrPath, string body, string secret, bool alreadyPathOnly = false)
        {
            var path = alreadyPathOnly ? urlOrPath : new Uri(urlOrPath).AbsolutePath;
            var payload = path + body;
            payload = JsStringEscape(payload);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string JsStringEscape(string s)
        {
            s = s.Replace("\\", "\\\\");
            s = s.Replace("\"", "\\\"");
            s = Regex.Replace(s, " ", "\\0");
            return s;
        }

        private sealed class IkhokhaConfig
        {
            public string AppId { get; set; } = string.Empty;
            public string AppSecret { get; set; } = string.Empty;
        }
    }

    public class IkhokhaPaymentLinkResponse
    {
        [JsonPropertyName("responseCode")]
        public string ResponseCode { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("paylinkUrl")]
        public string PaylinkUrl { get; set; } = string.Empty;

        [JsonPropertyName("paylinkID")]
        public string PaylinkID { get; set; } = string.Empty;

        [JsonPropertyName("externalTransactionID")]
        public string ExternalTransactionID { get; set; } = string.Empty;

        public bool IsSuccessfullyCreated => ResponseCode == "00" && !string.IsNullOrEmpty(PaylinkUrl);
    }

    public class IkhokhaStatusResponse
    {
        [JsonPropertyName("paylinkID")]
        public string PaylinkID { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("externalTransactionID")]
        public string? ExternalTransactionID { get; set; }

        // iKhokha returns "PAID" on getStatus and "SUCCESS" on the webhook — accept both.
        public bool IsPaid => string.Equals(Status, "PAID", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
    }

    public class IkhokhaWebhookPayload
    {
        [JsonPropertyName("paylinkID")]
        public string PaylinkID { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("externalTransactionID")]
        public string ExternalTransactionID { get; set; } = string.Empty;

        [JsonPropertyName("responseCode")]
        public string? ResponseCode { get; set; }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public class YocoCheckoutService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string YocoCheckoutUrl = "https://payments.yoco.com/api/checkouts";

        public YocoCheckoutService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value;
        }

        public async Task<YocoCheckoutResponse?> CreateCheckoutAsync(decimal amountInRands, string description, string successUrl, string cancelUrl, string failureUrl, Dictionary<string, string>? metadata = null)
        {
            var secretKey = await GetSettingAsync("Yoco:SecretKey");
            if (string.IsNullOrEmpty(secretKey))
                return null;

            var amountInCents = (int)(amountInRands * 100);

            var requestBody = new Dictionary<string, object>
            {
                { "amount", amountInCents },
                { "currency", "ZAR" },
                { "successUrl", successUrl },
                { "cancelUrl", cancelUrl },
                { "failureUrl", failureUrl }
            };

            if (!string.IsNullOrEmpty(description))
                requestBody["description"] = description;

            if (metadata != null)
                requestBody["metadata"] = metadata;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(YocoCheckoutUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<YocoCheckoutResponse>(responseBody, options);
        }
    }

    public class YocoCheckoutResponse
    {
        public string Id { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string RedirectUrl { get; set; } = null!;
    }
}

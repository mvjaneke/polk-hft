using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public class SahftaMembersClient
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpFactory;

        public SahftaMembersClient(ApplicationDbContext context, IHttpClientFactory httpFactory)
        {
            _context = context;
            _httpFactory = httpFactory;
        }

        public class MemberDto
        {
            public string? firstName { get; set; }
            public string? surname { get; set; }
            public string? membershipNumber { get; set; }
            public string? ageGroup { get; set; }
            public string? shootingDiscipline { get; set; }
            public string? club { get; set; }
            public string? province { get; set; }
            public bool isPaidUp { get; set; }
            public string? leaderboardDivision { get; set; }
        }

        private class ApiResponse
        {
            public string? season { get; set; }
            public int matchCount { get; set; }
            public List<MemberDto> members { get; set; } = new();
        }

        public async Task<MemberDto?> LookupByMembershipNumberAsync(string membershipNumber, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(membershipNumber)) return null;
            var url = $"/api/public/members?membershipNumber={Uri.EscapeDataString(membershipNumber.Trim())}";
            var result = await CallAsync(url, ct);
            return result?.members.FirstOrDefault();
        }

        public async Task<MemberDto?> LookupByNameAsync(string firstName, string surname, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(surname)) return null;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(firstName)) parts.Add($"firstName={Uri.EscapeDataString(firstName.Trim())}");
            if (!string.IsNullOrWhiteSpace(surname)) parts.Add($"surname={Uri.EscapeDataString(surname.Trim())}");
            var url = "/api/public/members?" + string.Join("&", parts);
            var result = await CallAsync(url, ct);
            // When one match, return it; when multiple, return the first (caller fall back to registration data if ambiguous).
            if (result == null) return null;
            return result.matchCount == 1 ? result.members.FirstOrDefault() : null;
        }

        private async Task<ApiResponse?> CallAsync(string relativeUrl, CancellationToken ct)
        {
            var baseUrl = await GetBaseUrlAsync();
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            try
            {
                var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(6);
                var fullUrl = baseUrl.TrimEnd('/') + relativeUrl;
                var response = await client.GetAsync(fullUrl, ct);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<ApiResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null; // fail silent — caller falls back to registration data
            }
        }

        private async Task<string?> GetBaseUrlAsync()
        {
            var s = await _context.SiteSettings.FirstOrDefaultAsync(x => x.Key == "Sahfta:ApiBaseUrl");
            return s?.Value;
        }
    }
}

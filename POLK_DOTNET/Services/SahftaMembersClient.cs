using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POLK_DOTNET.Data;

namespace POLK_DOTNET.Services
{
    public class SahftaMembersClient
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<SahftaMembersClient> _logger;

        public SahftaMembersClient(ApplicationDbContext context, IHttpClientFactory httpFactory, ILogger<SahftaMembersClient> logger)
        {
            _context = context;
            _httpFactory = httpFactory;
            _logger = logger;
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
            if (result == null) return null;

            // The API does case-insensitive CONTAINS matching, so multiple rows can come back even for a "unique" name.
            // Prefer an exact case-insensitive match on both firstName and surname; fall back to the lone match otherwise.
            var fn = firstName?.Trim() ?? "";
            var sn = surname?.Trim() ?? "";
            var exact = result.members.FirstOrDefault(m =>
                string.Equals(m.firstName?.Trim(), fn, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.surname?.Trim(), sn, StringComparison.OrdinalIgnoreCase));

            if (exact != null) return exact;
            if (result.matchCount == 1) return result.members.FirstOrDefault();
            _logger.LogInformation("SAHFTA name lookup '{First} {Surname}' returned {Count} matches but no exact match; skipping.", fn, sn, result.matchCount);
            return null;
        }

        private async Task<ApiResponse?> CallAsync(string relativeUrl, CancellationToken ct)
        {
            var baseUrl = await GetBaseUrlAsync();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogWarning("SAHFTA lookup skipped — Sahfta:ApiBaseUrl is not configured in Site Settings.");
                return null;
            }

            var fullUrl = baseUrl.TrimEnd('/') + relativeUrl;
            try
            {
                var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(6);
                var response = await client.GetAsync(fullUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SAHFTA lookup {Url} returned HTTP {Status}.", fullUrl, (int)response.StatusCode);
                    return null;
                }
                var json = await response.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<ApiResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.LogInformation("SAHFTA lookup {Url} -> {Count} match(es).", fullUrl, parsed?.matchCount ?? 0);
                return parsed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SAHFTA lookup {Url} failed.", fullUrl);
                return null;
            }
        }

        private async Task<string?> GetBaseUrlAsync()
        {
            var s = await _context.SiteSettings.FirstOrDefaultAsync(x => x.Key == "Sahfta:ApiBaseUrl");
            return s?.Value;
        }
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POLK_DOTNET.Data;
using POLK_DOTNET.Services;

namespace POLK_DOTNET.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class YocoWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<YocoWebhookController> _logger;
        private readonly MembershipService _membership;

        public YocoWebhookController(ApplicationDbContext context, ILogger<YocoWebhookController> logger, MembershipService membership)
        {
            _context = context;
            _logger = logger;
            _membership = membership;
        }

        [HttpPost]
        public async Task<IActionResult> HandleWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Yoco webhook received: {Body}", body);

            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(body);

                var type = payload.GetProperty("type").GetString();
                if (type != "payment.succeeded")
                    return Ok();

                var data = payload.GetProperty("payload");

                if (data.GetProperty("metadata").TryGetProperty("applicationId", out var appIdElement))
                {
                    if (int.TryParse(appIdElement.GetString(), out int applicationId))
                    {
                        await _membership.MarkPaidAsync(applicationId, "Online");
                        _logger.LogInformation("Membership application {Id} paid via Yoco webhook", applicationId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Yoco webhook");
            }

            return Ok();
        }
    }
}

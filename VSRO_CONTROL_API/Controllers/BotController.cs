using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using VSRO_CONTROL_API.Attributes;

namespace VSRO_CONTROL_API.Controllers
{
    /// <summary>
    /// This bot controller connects to 192.168.0.153, for the bot api. 
    /// The bot api is not open source, as theres nothing really resourceful contained.
    /// Everything done is equivalent to how i handle SMC automation in this AIO API.
    /// The bot api is only connectable locally, we are the gatekeeper to its traffic.
    /// -------------------------------------------------------------------------------
    /// This is deprecated, and to be replaced with new self contained bot logic.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BotController : Controller
    {
        private static readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.0.153:5000")
        };

        [RequireAdmin]
        [HttpPost("start-bots")]
        public async Task<IActionResult> StartBots()
        {
            try
            {
                var response = await _http.PostAsync("/bot/start", null);
                var body = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { message = body.Trim('"') });
            }
            catch (Exception ex)
            {
                return BadRequest();
            }
            
        }

        [RequireAdmin]
        [HttpPost("stop-bots")]
        public async Task<IActionResult> StopBots()
        {
            try
            {
                var response = await _http.PostAsync("/bot/stop", null);
                var body = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { message = body.Trim('"') });
            }
            catch (Exception ex)
            {
                // Dont log right now. 
                return BadRequest();
            }
            
        }

        [RequireAdmin]
        [HttpGet("bot-status")]
        public async Task<IActionResult> BotStatus()
        {

            try
            {
                var response = await _http.GetAsync("/bot/status");
                var body = await response.Content.ReadAsStringAsync();
                // Status returns a full JSON object, forward it raw
                return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<JsonElement>(body));
            }
            catch (Exception ex)
            {
                // Dont log right now.
                return BadRequest();
            }
        }

        [RequireAdmin]
        [HttpPost("stop-bot/{name}")]
        public async Task<IActionResult> StopOne(string name)
        {
            var response = await _http.PostAsync($"/bot/stop/{name}", null);
            var body = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, new { message = body.Trim('"') });
        }

        [RequireAdmin]
        [HttpPost("restart-bot/{name}")]
        public async Task<IActionResult> RestartOne(string name)
        {
            var response = await _http.PostAsync($"/bot/restart/{name}", null);
            var body = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, new { message = body.Trim('"') });
        }

        [RequireAdmin]
        [HttpPost("bot-trainplace/{name}")]
        public async Task<IActionResult> SetTrainplace(string name, [FromBody] JsonElement body)
        {
            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"/bot/trainplace/{name}", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, new { message = responseBody.Trim('"') });
        }

        [RequireAdmin]
        [HttpGet("bot-trainplace/{name}")]
        public async Task<IActionResult> GetTrainplace(string name)
        {
            var response = await _http.GetAsync($"/bot/trainplace/{name}");
            var body = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<JsonElement>(body));
        }
    }
}
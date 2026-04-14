using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class PlayersController : ControllerBase
    {
        public record CreateAccountRequest(string Username, string Password, bool IsAdmin);
        public record AddSilkRequest(string Username, int Amount);

        // GET api/players/public-player-count — no auth required, safe for public pages
        [AllowAnonymous]
        [HttpGet("public-player-count")]
        public async Task<IActionResult> GetPublicPlayerCount()
        {
            var (success, reason) = await DBConnect.GetOnlineUsers();
            if (!success) return Ok(new { count = "—" });
            return Ok(new { count = reason });
        }

        // GET api/players/online-count
        [HttpGet("online-count")]
        public async Task<IActionResult> GetOnlineCount()
        {
            if (Overseer.AgentProxy == null
                || Overseer.GatewayProxy == null
                || Overseer.DownloadProxy == null) return Ok(new { count = "0" });

            // For future, might want to track every module one day.
            List<Proxy> playing = Overseer.AgentProxy.Connections.Values.ToList();
            List<Proxy> loggingIn = Overseer.GatewayProxy.Connections.Values.ToList();
            List<Proxy> updating = Overseer.DownloadProxy.Connections.Values.ToList();

            int count = playing.Count;
            string countStr = $"{count}";

            return Ok(new { count = countStr });
        }

        // GET api/players/character-position?name=
        [HttpGet("character-position")]
        public async Task<IActionResult> GetCharacterPosition([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { message = "Character name is required." });

            var (success, data, reason) = await DBConnect.GetCharacterPositionAsync(name);
            if (!success) return NotFound(new { message = reason });
            return Ok(data);
        }

        // POST api/players/account
        [HttpPost("account")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
                return BadRequest(new { message = "Username and password are required." });

            var (success, reason) = await DBConnect.AddNewUser(body.Username, body.Password, body.IsAdmin);
            if (!success) return BadRequest(new { message = reason });
            return Ok(new { message = $"Account '{body.Username}' created successfully." });
        }

        // POST api/players/silk
        [HttpPost("silk")]
        public async Task<IActionResult> AddSilk([FromBody] AddSilkRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Username))
                return BadRequest(new { message = "Username is required." });

            var (success, reason) = await DBConnect.AddSilkToUserByName(body.Username, body.Amount);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = $"Added {body.Amount} silk to '{body.Username}'." });
        }

        // DELETE api/players/characters/{jid}
        [HttpDelete("characters/{jid:int}")]
        public async Task<IActionResult> TruncateCharacters(int jid)
        {
            if (jid <= 0)
                return BadRequest(new { message = "A valid JID is required." });

            var (success, reason) = await DBConnect.TruncateCharactersByJID(jid);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO.ServerCfg;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/server-cfg")]
    public class ServerCfgController : ControllerBase
    {
        private static readonly string GameBlock  = "SR_GameServer";
        private static readonly string ShardBlock = "SR_ShardManager";

        // GET api/server-cfg/rates — public, used by the about page
        [AllowAnonymous]
        [HttpGet("rates")]
        public IActionResult GetRates()
        {
            var parser = ServerCfgParser.Instance;
            if (parser == null)
                return StatusCode(503, new { message = "server.cfg not loaded. Check ServerCfgPath in settings." });

            return Ok(new ServerRates(
                parser.GetInt(GameBlock, "ExpRatio"),
                parser.GetInt(GameBlock, "ExpRatioParty"),
                parser.GetInt(GameBlock, "DropItemRatio"),
                parser.GetInt(GameBlock, "DropGoldAmountCoef"),
                string.Equals(parser.Get(GameBlock,  "WINTER_EVENT_2009"),   "EVENT_ON", StringComparison.OrdinalIgnoreCase),
                string.Equals(parser.Get(GameBlock,  "THANKS_GIVING_EVENT"), "EVENT_ON", StringComparison.OrdinalIgnoreCase),
                parser.GetInt(ShardBlock, "ChristmasEvent2007") != 0
            ));
        }

        // PUT api/server-cfg/rates — admin only, writes back to server.cfg
        [RequireAdmin]
        [HttpPut("rates")]
        public async Task<IActionResult> UpdateRates([FromBody] ServerRates req)
        {
            var parser = ServerCfgParser.Instance;
            if (parser == null)
                return StatusCode(503, new { message = "server.cfg not loaded. Check ServerCfgPath in settings." });

            try
            {
                await parser.UpdateValueAsync(GameBlock,  "ExpRatio",            req.ExpRatio.ToString());
                await parser.UpdateValueAsync(GameBlock,  "ExpRatioParty",       req.ExpRatioParty.ToString());
                await parser.UpdateValueAsync(GameBlock,  "DropItemRatio",       req.DropItemRatio.ToString());
                await parser.UpdateValueAsync(GameBlock,  "DropGoldAmountCoef",  req.DropGoldAmountCoef.ToString());
                await parser.UpdateValueAsync(GameBlock,  "WINTER_EVENT_2009",   req.WinterEvent2009    ? "EVENT_ON" : "EVENT_OFF");
                await parser.UpdateValueAsync(GameBlock,  "THANKS_GIVING_EVENT", req.ThanksgivingEvent  ? "EVENT_ON" : "EVENT_OFF");
                await parser.UpdateValueAsync(ShardBlock, "ChristmasEvent2007",  req.ChristmasEvent2007 ? "1" : "0");
                return Ok(new { message = "Rates updated in server.cfg." });
            }
            catch (KeyNotFoundException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // PUT api/server-cfg/certification-ip — admin only, updates all Certification IPs
        [RequireAdmin]
        [HttpPut("certification-ip")]
        public async Task<IActionResult> UpdateCertificationIP([FromBody] UpdateIpRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Ip))
                return BadRequest(new { message = "IP address is required." });

            var parser = ServerCfgParser.Instance;
            if (parser == null)
                return StatusCode(503, new { message = "server.cfg not loaded." });

            try
            {
                await parser.UpdateAllCertificationIPsAsync(req.Ip);
                return Ok(new { message = $"All Certification IPs updated to {req.Ip}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public record UpdateIpRequest(string Ip);
    }
}

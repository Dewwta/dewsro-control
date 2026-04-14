using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.Enums;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class WorldController : ControllerBase
    {
        public record AddNpcRequest(
            string CodeName,
            string CharacterName,
            int StoreGroups,
            int TabGroup1,
            int TabGroup2,
            int TabGroup3,
            int TabGroup4,
            int LookingDir);

        public record AddReversePointRequest(string ZoneName, int PosX, int PosY, int PosZ, int RegionId);

        public record AddTeleporterRequest(
            string CodeName,
            int GoldFee,
            Town TownLink,
            string FromCharacter,
            string ToCharacter,
            int RequiredLevel);

        public record MonsterSpawnsRequest(string MonsterCodeName, int MaxCount, bool Exact = false);
        public record AllGroupSpawnsRequest(int MaxCount);

        // POST api/world/npc
        [HttpPost("npc")]
        public async Task<IActionResult> AddNpc([FromBody] AddNpcRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.CodeName) || string.IsNullOrWhiteSpace(body.CharacterName))
                return BadRequest(new { message = "CodeName and CharacterName are required." });

            var (success, reason) = await DBConnect.AddNewNPCToCharacterLocation(
                body.CodeName, body.CharacterName,
                body.StoreGroups,
                body.TabGroup1, body.TabGroup2, body.TabGroup3, body.TabGroup4,
                body.LookingDir);

            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // POST api/world/reverse-point
        [HttpPost("reverse-point")]
        public async Task<IActionResult> AddReversePoint([FromBody] AddReversePointRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.ZoneName))
                return BadRequest(new { message = "ZoneName is required." });

            var (success, reason) = await DBConnect.AddReversePointToCharacterPosition(
                body.ZoneName, body.PosX, body.PosY, body.PosZ, body.RegionId);

            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // POST api/world/teleporter
        [HttpPost("teleporter")]
        public async Task<IActionResult> AddTeleporter([FromBody] AddTeleporterRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.CodeName) || string.IsNullOrWhiteSpace(body.FromCharacter) || string.IsNullOrWhiteSpace(body.ToCharacter))
                return BadRequest(new { message = "CodeName, FromCharacter, and ToCharacter are required." });

            var (success, reason) = await DBConnect.AddNewTeleporter(
                body.CodeName, body.GoldFee, body.TownLink,
                body.FromCharacter, body.ToCharacter, body.RequiredLevel);

            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // PUT api/world/monster-spawns
        [HttpPut("monster-spawns")]
        public async Task<IActionResult> SetMonsterSpawns([FromBody] MonsterSpawnsRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.MonsterCodeName))
                return BadRequest(new { message = "MonsterCodeName is required." });

            var (success, reason) = await DBConnect.ChangeMonsterSpawns(body.MonsterCodeName, body.MaxCount, body.Exact);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // PUT api/world/monster-spawns/all-groups
        [HttpPut("monster-spawns/all-groups")]
        public async Task<IActionResult> SetAllGroupMonsterSpawns([FromBody] AllGroupSpawnsRequest body)
        {
            var (success, reason) = await DBConnect.ChangeMonsterSpawnsAllGroups(body.MaxCount);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }

        // POST api/world/fix-unique-spawns
        [HttpPost("fix-unique-spawns")]
        public async Task<IActionResult> FixUniqueSpawns()
        {
            var (success, reason) = await DBConnect.FixUniqueSpawns();
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = "Unique spawns corrected to max 1 instance each." });
        }

        public record SetRegionRequest(string AreaName, bool Enabled);

        // GET api/world/regions
        [HttpGet("regions")]
        public async Task<IActionResult> GetRegions()
        {
            var (success, items, reason) = await DBConnect.GetRegionAssoc();
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(items!.Select(r => new { areaName = r.AreaName, enabled = r.Enabled }));
        }

        // GET api/world/monster-spawn-counts?prefix=MOB_CH_
        [HttpGet("monster-spawn-counts")]
        public async Task<IActionResult> GetMonsterSpawnCounts([FromQuery] string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return BadRequest(new { message = "prefix is required." });

            var (success, reason, data) = await DBConnect.GetMonsterSpawnCounts(prefix);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(data!.Select(d => new { mobCode = d.MobCode, maxCount = d.MaxCount }));
        }

        // PUT api/world/regions
        [HttpPut("regions")]
        public async Task<IActionResult> SetRegion([FromBody] SetRegionRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.AreaName))
                return BadRequest(new { message = "AreaName is required." });

            var (success, reason) = await DBConnect.SetRegionAssoc(body.AreaName, body.Enabled);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }
    }
}

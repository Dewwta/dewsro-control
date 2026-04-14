using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class LiveController : ControllerBase
    {
        // GET api/live/sessions
        // Returns all proxy connections that have an active player session.
        [HttpGet("sessions")]
        public IActionResult GetSessions()
        {
            if (Overseer.AgentProxy == null)
                return Ok(Array.Empty<LiveSessionDTO>());

            var sessions = Overseer.AgentProxy.Connections.Values
                .Where(p => p.Session != null)
                .Select(p => new LiveSessionDTO
                {
                    ConnectionId   = p.ConnectionId,
                    CharacterName  = p.Session!.CharacterName ?? "",
                    JID            = p.Session.JID,
                    IP             = p.Session.IP ?? "",
                    LoginTime      = p.Session.LoginTime,
                    SessionSeconds = p.Session.AccumulatedPlayTime.TotalSeconds,
                    IsAfk          = p.Session.IsAfk,
                    InventoryReady = p.Inventory.IsReady,
                    Party          = p.Session.PlayerParty == null ? null : new LivePartyDTO
                    {
                        PartyId     = p.Session.PlayerParty.PartyID,
                        Message     = p.Session.PlayerParty.Message,
                        LeaderName  = p.Session.PlayerParty.Leader?.Session?.CharacterName,
                        MemberNames = p.Session.PlayerParty.Members
                            .Select(m => m.Session?.CharacterName ?? "?")
                            .ToList()
                    },
                    Stats = p.Session.PlayerStats == null ? null : new LiveStatsDTO
                    {
                        STR              = p.Session.PlayerStats.STR,
                        INT              = p.Session.PlayerStats.INT,
                        Level            = p.Session.PlayerStats.CurrentLevel,
                        CurrentHP        = p.Session.PlayerStats.CurrentHP,
                        CurrentMP        = p.Session.PlayerStats.CurrentMP,
                        ZerkLevel        = p.Session.PlayerStats.ZerkLevel,
                        UnusedStatPoints = p.Session.PlayerStats.UnusedStatPoints,
                        Gold             = p.Session.PlayerStats.RemainingGold,
                        SkillPoints      = p.Session.PlayerStats.RemainingSkillPoints
                    }
                })
                .ToList();

            return Ok(sessions);
        }

        // GET api/live/sessions/{connectionId}/inventory
        // Returns full inventory (equipment + bag + pets) with display names and icon URLs.
        [HttpGet("sessions/{connectionId:int}/inventory")]
        public async Task<IActionResult> GetInventory(int connectionId)
        {
            if (Overseer.AgentProxy == null)
                return NotFound(new { message = "Proxy is not running." });

            if (!Overseer.AgentProxy.Connections.TryGetValue(connectionId, out var proxy) || proxy.Session == null)
                return NotFound(new { message = "Session not found." });

            var inv = proxy.Inventory;

            // Collect every unique code name so we can batch-query icon paths
            var allCodeNames = inv.Equipment.Values.Select(v => v.CodeName)
                .Concat(inv.Slots.Values.Select(v => v.CodeName))
                .Concat(inv.Pets.Values
                    .Where(d => d != null)
                    .SelectMany(d => d.Inventory.Values ?? Enumerable.Empty<(int ItemID, string CodeName, int Stack, int MaxStack)>())
                    .Select(v => v.CodeName))
                .Where(c => !string.IsNullOrEmpty(c));

            var iconPaths = await DBConnect.GetItemIconPaths(allCodeNames);

            var dto = new LiveInventoryDTO
            {
                ConnectionId  = connectionId,
                CharacterName = proxy.Session.CharacterName ?? "",
                Equipment = inv.Equipment
                    .OrderBy(kv => kv.Key)
                    .Select(kv => BuildItem(kv.Key, kv.Value, iconPaths))
                    .ToList(),
                Inventory = inv.Slots
                    .OrderBy(kv => kv.Key)
                    .Select(kv => BuildItem(kv.Key, kv.Value, iconPaths))
                    .ToList(),
                Pets = inv.Pets.ToDictionary(
                    kv => kv.Key.ToString("X"),
                    kv =>
                    {
                        var list = kv.Value.Inventory
                            .OrderBy(sv => sv.Key)
                            .Select(sv => BuildItem(sv.Key, sv.Value, iconPaths))
                            .ToList();

                        return list;
                    }
                ),
                PetInfos = inv.Pets.ToDictionary(
                    kv => kv.Key.ToString("X"),
                    kv =>
                    {
                        return new PetInfo
                        {
                            Name = inv.Pets[kv.Key].Info.Name,
                            IsAttackPet = inv.Pets[kv.Key].IsAttackPet,
                            CodeName = inv.Pets[kv.Key].Info.CodeName,
                            ReadableName = inv.Pets[kv.Key].Info.ReadableName,
                        };
                    }
                )
            };

            return Ok(dto);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static LiveInventoryItemDTO BuildItem(
            byte slot,
            (int ItemID, string CodeName, int Stack, int MaxStack) item,
            Dictionary<string, string> iconPaths)
        {
            if (item.ItemID == 0 || string.IsNullOrEmpty(item.CodeName))
            {
                return new LiveInventoryItemDTO
                {
                    Slot = slot,
                    ItemId = 0,
                    CodeName = "",
                    DisplayName = "Empty",
                    Stack = 0,
                    MaxStack = 0,
                    IconUrl = null
                };
            }

            iconPaths.TryGetValue(item.CodeName, out var rawIcon);

            return new LiveInventoryItemDTO
            {
                Slot = slot,
                ItemId = item.ItemID,
                CodeName = item.CodeName,
                DisplayName = GameObjectNameResolver.Resolve(item.CodeName),
                Stack = item.Stack,
                MaxStack = item.MaxStack,
                IconUrl = rawIcon != null
                    ? "/Icon/" + rawIcon.Replace('\\', '/').Replace(".ddj", ".png", StringComparison.OrdinalIgnoreCase)
                    : null
            };
        }
    }
}

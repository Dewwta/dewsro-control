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
                    IsGM           = p.Session.IsGM,
                    InventoryReady = p.Session.Inventory.IsReady,
                    Party          = p.Session.PlayerParty == null ? null : new LivePartyDTO
                    {
                        PartyId     = p.Session.PlayerParty.PartyID,
                        Message     = p.Session.PlayerParty.Message,
                        LeaderName  = p.Session.PlayerParty.Leader?.Session?.CharacterName,
                        MemberNames = p.Session.PlayerParty.Members
                            .Select(m => m.Session?.CharacterName ?? "?")
                            .ToList()
                    },
                    Stats = new LiveStatsDTO
                    {
                        // PlayerStats fields — safe with null-check
                        STR              = p.Session.PlayerStats?.STR ?? 0,
                        INT              = p.Session.PlayerStats?.INT ?? 0,
                        Level            = p.Session.PlayerStats?.CurrentLevel ?? 0,
                        CurrentHP        = p.Session.PlayerStats?.CurrentHP ?? 0,
                        MaxHP            = p.Session.PlayerStats?.MaxHP ?? 0,
                        CurrentMP        = p.Session.PlayerStats?.CurrentMP ?? 0,
                        MaxMP            = p.Session.PlayerStats?.MaxMP ?? 0,
                        ZerkLevel        = p.Session.PlayerStats?.ZerkLevel ?? 0,
                        UnusedStatPoints = p.Session.PlayerStats?.UnusedStatPoints ?? 0,
                        Gold             = p.Session.PlayerStats?.RemainingGold ?? 0,
                        SkillPoints      = p.Session.PlayerStats?.RemainingSkillPoints ?? 0,

                        // World position
                        CurrentRegion      = p.Session.RegionReadableName ?? "N/A",
                        WorldX             = p.Session.WorldX,
                        WorldY             = p.Session.WorldY,
                        WorldZ             = p.Session.Z,

                        // Identity & race
                        Race               = p.Session.CharacterRace.ToString(),
                        ServerName         = p.Session.ServerName,
                        CharacterUID       = p.Session.CharacterUID,
                        AgentSessionId     = p.Session.AgentSessionId,

                        // Movement
                        IsMoving           = p.Session.IsMoving,
                        RunSpeed           = p.Session.RunSpeed,
                        SectorX            = p.Session.SectorX,
                        SectorY            = p.Session.SectorY,
                        RegionId           = p.Session.RegionId,
                        RegionName         = p.Session.RegionName,
                        RegionReadableName = p.Session.RegionReadableName,

                        // Session statistics
                        SessionKills       = p.Session.SessionKills,
                        SessionUniqueKills = p.Session.SessionUniqueKills,
                        CumulativeExp      = p.Session.CumulativeExp,
                        LastTargetUID      = p.Session.LastTargetUID,

                        // Bot state
                        BotState             = p.Session._botState.ToString(),
                        TrainingDestX        = p.Session.TrainingDestination?.X,
                        TrainingDestY        = p.Session.TrainingDestination?.Y,
                        TrainingDestZ        = p.Session.TrainingDestination?.ZOffset,
                        TrainingDestRegionId = p.Session.TrainingDestination?.RegionId,

                        // World tracking counts
                        SpawnedObjectCount = p.Session.SpawnedObjects.Count,
                        MobCount           = p.Session.MobUIDs.Count,
                        DroppedItemCount   = p.Session.DroppedItems.Count,
                    }
                })
                .ToList();

            return Ok(sessions);
        }

        // GET api/live/sessions/{connectionId}/inventory
        // Returns full inventory with display names and icon URLs.
        [HttpGet("sessions/{connectionId:int}/inventory")]
        public async Task<IActionResult> GetInventory(int connectionId)
        {
            if (Overseer.AgentProxy == null)
                return NotFound(new { message = "Proxy is not running." });

            if (!Overseer.AgentProxy.Connections.TryGetValue(connectionId, out var proxy) || proxy.Session == null)
                return NotFound(new { message = "Session not found." });

            var inv = proxy.Session.Inventory;

            // collect every unique code name to batch-query icon paths
            var allCodeNames = inv.Equipment.Values.Select(v => v.CodeName128)
                .Concat(inv.Slots.Values.Select(v => v.CodeName128))
                .Concat(inv.Pets.Values
                    .Where(d => d != null)
                    .SelectMany(d => d.Inventory.Values ?? Enumerable.Empty<SR_Item>())
                    .Select(v => v.CodeName128))
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

        private static LiveInventoryItemDTO BuildItem(
            byte slot,
            SR_Item item,
            Dictionary<string, string> iconPaths)
        {
            if (item.RefItemID == 0 || string.IsNullOrEmpty(item.CodeName128))
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

            iconPaths.TryGetValue(item.CodeName128, out var rawIcon);

            return new LiveInventoryItemDTO
            {
                Slot = slot,
                ItemId = item.RefItemID,
                CodeName = item.CodeName128,
                DisplayName = GameObjectNameResolver.Resolve(item.CodeName128),
                Stack = item.Stack,
                MaxStack = item.MaxStack,
                IconUrl = rawIcon != null
                    ? "/Icon/" + rawIcon.Replace('\\', '/').Replace(".ddj", ".png", StringComparison.OrdinalIgnoreCase)
                    : null
            };
        }
    }
}

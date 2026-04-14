using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/characters")]
    public class CharacterController : ControllerBase
    {
        // GET /api/characters
        // For each character owned by the authenticated user:
        //   - If currently online in the proxy, build a live snapshot from the session.
        //   - Otherwise, fall back to the last persisted snapshot file.
        [RequireAuth]
        [HttpGet]
        public async Task<IActionResult> GetMyCharacters()
        {
            var user = HttpContext.Items["User"] as UserDTO;
            if (user == null) return Unauthorized(new { message = "Authentication required." });

            var res = await DBConnect.GetCharactersByJID(user.JID);
            if (!res.success)
                return StatusCode(500, new { message = res.reason });

           

            var result = res.characters.Select(c =>
            {
                // find a live proxy connection character
                var liveProxy = Overseer.AgentProxy?.Connections.Values
                    .FirstOrDefault(p =>
                        string.Equals(
                            p.Session?.CharacterName?.Trim(),
                            c.CharName?.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        ));

                bool isOnline = liveProxy != null;

                CharacterSnapshot? state = isOnline
                    ? BuildLiveSnapshot(liveProxy!)
                    : CharacterSnapshotStore.GetByName(c.CharName);

                return new CharacterWithStateDTO
                {
                    Login          = c.Login,
                    CharName       = c.CharName,
                    SilkOwn        = c.SilkOwn,
                    SilkGift       = c.SilkGift,
                    JID            = c.JID,
                    CharID         = c.CharID,
                    IsOnline       = isOnline,
                    LastKnownState = state,
                };
            }).ToList();

            return Ok(result);
        }

        // GET /api/characters/{charId}/snapshot
        // Returns the state for a single character — live if online, persisted if not.
        [RequireAuth]
        [HttpGet("{charName}/snapshot")]
        public IActionResult GetSnapshot(string charName)
        {
            var user = HttpContext.Items["User"] as UserDTO;
            if (user == null) return Unauthorized(new { message = "Authentication required." });

            var liveProxy = Overseer.AgentProxy?.Connections.Values
                .FirstOrDefault(p =>
                    p.Session?.CharacterName == charName);

            var snapshot = liveProxy != null
                ? BuildLiveSnapshot(liveProxy)
                : CharacterSnapshotStore.GetByName(charName);

            if (snapshot == null)
                return NotFound(new { message = "No snapshot found for this character." });

            // non-admins may only view their own character snapshots
            if (!user.IsAuthoritive() && snapshot.JID != user.JID)
                return Forbid();

            return Ok(snapshot);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a CharacterSnapshot from a live proxy connection.
        /// SavedAt is set to UtcNow to signal this is a real-time reading.
        /// </summary>
        private static CharacterSnapshot BuildLiveSnapshot(Proxy proxy)
        {
            var session = proxy.Session!;
            var inv     = proxy.Inventory;

            return new CharacterSnapshot
            {
                CharacterName    = session.CharacterName ?? "",
                CharacterID      = session.CharacterID,
                JID              = session.JID,
                SavedAt          = DateTime.UtcNow,   // live — always "now"

                Level            = session.PlayerStats?.CurrentLevel    ?? 0,
                CurrentHP        = session.PlayerStats?.CurrentHP       ?? 0,
                CurrentMP        = session.PlayerStats?.CurrentMP       ?? 0,
                ZerkLevel        = session.PlayerStats?.ZerkLevel       ?? 0,
                UnusedStatPoints = session.PlayerStats?.UnusedStatPoints ?? 0,
                Gold             = session.PlayerStats?.RemainingGold   ?? 0,
                SkillPoints      = session.PlayerStats?.RemainingSkillPoints ?? 0,
                
                Equipment = inv.Equipment.ToDictionary(
                    kv => kv.Key,
                    kv => ToItem(kv.Value)),

                Slots = inv.Slots.ToDictionary(
                    kv => kv.Key,
                    kv => ToItem(kv.Value)),

                Pets = inv.Pets.ToDictionary(
                    kv => kv.Key.ToString("X"),
                    kv => kv.Value.Inventory.ToDictionary(
                        sv => sv.Key,
                        sv => ToItem(sv.Value))),
            };
        }

        private static SnapshotItem ToItem(
            (int ItemID, string CodeName, int Stack, int MaxStack) t) =>
            new()
            {
                ItemID   = t.ItemID,
                CodeName = t.CodeName,
                Stack    = t.Stack,
                MaxStack = t.MaxStack,
            };
    }
}

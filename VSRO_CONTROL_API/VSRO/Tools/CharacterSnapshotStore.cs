using CoreLib.Tools.Logging;
using System.Text.Json;
using VSRO_CONTROL_API.VSRO.DTO;
using static System.Collections.Specialized.BitVector32;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public static class CharacterSnapshotStore
    {
        private static readonly string _dir =
            Path.Combine(AppContext.BaseDirectory, "data", "snapshots");

        private static readonly JsonSerializerOptions _writeOpts =
            new() { WriteIndented = true };

        /// <summary>Saves a snapshot for a character on logoff. Overwrites any previous file.</summary>
        public static void Save(PlayerSession session, InventoryTracker inventory)
        {
            try
            {
                if (session.CharacterID == 0)
                {
                    Logger.Warn(typeof(CharacterSnapshotStore),
                        $"Skipping snapshot for {session.CharacterName}: CharacterID is 0");
                    return;
                }

                Directory.CreateDirectory(_dir);

                var snapshot = new CharacterSnapshot
                {
                    CharacterName    = session.CharacterName ?? "",
                    CharacterID      = session.CharacterID,
                    JID              = session.JID,
                    SavedAt          = DateTime.UtcNow,

                    Level            = session.PlayerStats?.CurrentLevel ?? 0,
                    CurrentHP        = session.PlayerStats?.CurrentHP ?? 0,
                    CurrentMP        = session.PlayerStats?.CurrentMP ?? 0,
                    ZerkLevel        = session.PlayerStats?.ZerkLevel ?? 0,
                    UnusedStatPoints = session.PlayerStats?.UnusedStatPoints ?? 0,
                    Gold             = session.PlayerStats?.RemainingGold ?? 0,
                    SkillPoints      = session.PlayerStats?.RemainingSkillPoints ?? 0,

                    Equipment = inventory.Equipment.ToDictionary(
                        kv => kv.Key,
                        kv => ToItem(kv.Value)),

                    Slots = inventory.Slots.ToDictionary(
                        kv => kv.Key,
                        kv => ToItem(kv.Value)),

                    Pets = inventory.Pets.ToDictionary(
                        kv => kv.Key.ToString("X"),
                        kv => kv.Value.Inventory.ToDictionary(
                            sv => sv.Key,
                            sv => ToItem(sv.Value)))
                };

                string safeName = session.CharacterName ?? "unknown";
                string path = Path.Combine(_dir, $"{safeName}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(snapshot, _writeOpts));

                Logger.Info(typeof(CharacterSnapshotStore),
                    $"Snapshot saved for {session.CharacterName} (CharID={session.CharacterID})");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(CharacterSnapshotStore),
                    $"Failed to save snapshot for {session.CharacterName}: {ex.Message}");
            }
        }

        /// <summary>Loads the last saved snapshot for the given CharacterID.</summary>
        public static CharacterSnapshot? GetByName(string charName)
        {
            try
            {
                string path = Path.Combine(_dir, $"{charName}.json");
                if (!File.Exists(path)) return null;

                return JsonSerializer.Deserialize<CharacterSnapshot>(
                    File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
        private static SnapshotItem ToItem(
            (int ItemID, string CodeName, int Stack, int MaxStack) t) =>
            new()
            {
                ItemID   = t.ItemID,
                CodeName = t.CodeName,
                Stack    = t.Stack,
                MaxStack = t.MaxStack
            };
    }
}

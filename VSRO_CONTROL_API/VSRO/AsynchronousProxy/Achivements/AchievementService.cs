using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements
{
    public static class AchievementService
    {
        /// <summary>
        /// Call during startup after AchievementLoader.Load() and DB is ready.
        /// </summary>
        public static async Task<bool> Initialize()
        {
            if (!AchievementLoader.IsLoaded)
            {
                Logger.Info(typeof(AchievementService), "Cannot initialize — achievements not loaded.");
                return false;
            }

            bool tableReady = await DBConnect.InitAchievementTable();
            if (!tableReady)
            {
                Logger.Error(typeof(AchievementService), "Failed to initialize AchievementProgress table.");
                return false;
            }

            Logger.Info(typeof(AchievementService), "Achievement system initialized.");
            return true;
        }

        /// <summary>
        /// Call this from your kill packet handler.
        /// monsterCodeName = the CodeName128 of the killed mob.
        /// </summary>
        public static async Task OnMonsterKill(string charName, string monsterCodeName, Proxy proxy)
        {
            var killAchievements = AchievementLoader.GetByType("kill");

            foreach (var ach in killAchievements)
            {
                // If MonsterCodeName is specified, it must match
                if (ach.MonsterCodeName != null &&
                    !ach.MonsterCodeName.Equals(monsterCodeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool newlyCompleted = await DBConnect.IncrementAchievementProgress(
                    charName, ach.Name, 1, ach.Count);

                if (newlyCompleted)
                {
                    Logger.Info(typeof(AchievementService),
                        $"[ACH] {charName} completed: \"{ach.Name}\"!");

                    PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, $"ACHIEVEMENT UNLOCKED: {ach.Name}, {ach.Description}");
                }
            }
        }

        /// <summary>
        /// Get a full achievement overview for a character,
        /// merging XML definitions with DB progress.
        /// Useful for an API endpoint.
        /// </summary>
        public static async Task<List<AchievementStatus>> GetStatusForChar(string charName)
        {
            if (!AchievementLoader.IsLoaded || AchievementLoader.Definitions == null)
                return new();

            var dbProgress = await DBConnect.GetAllAchievementsForChar(charName);
            var progressLookup = dbProgress.ToDictionary(p => p.name, p => p);

            var result = new List<AchievementStatus>();

            foreach (var def in AchievementLoader.Definitions.Achievements)
            {
                var status = new AchievementStatus
                {
                    Name = def.Name,
                    Description = def.Description,
                    Type = def.Type,
                    Goal = def.Count,
                    MonsterCodeName = def.MonsterCodeName
                };

                if (progressLookup.TryGetValue(def.Name, out var p))
                {
                    status.Progress = p.progress;
                    status.Completed = p.completed;
                    status.CompletedAt = p.completedAt;
                }

                result.Add(status);
            }

            return result;
        }
    }

    /// <summary>
    /// DTO for API responses
    /// </summary>
    public class AchievementStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Goal { get; set; }
        public string? MonsterCodeName { get; set; }
        public long Progress { get; set; } = 0;
        public bool Completed { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
    }
}

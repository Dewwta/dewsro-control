using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements
{
    public static class AchievementService
    {
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

        // -----------------------------------------------------------------------
        // Core
        // -----------------------------------------------------------------------

        /// <summary>
        /// Internal: increment matching achievements and fire the unlock notice.
        /// </summary>
        private static async Task ProcessAchievements(
            string charName, Proxy proxy,
            IEnumerable<AchievementDefinitions.AchievementDefinition> candidates)
        {
            foreach (var ach in candidates)
            {
                bool newlyCompleted = await DBConnect.IncrementAchievementProgress(
                    charName, ach.Name, 1, ach.Count);

                if (newlyCompleted)
                {
                    Logger.Info(typeof(AchievementService),
                        $"[ACH] {charName} completed: \"{ach.Name}\"!");
                    PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null,
                        $"ACHIEVEMENT UNLOCKED: {ach.Name} — {ach.Description}");
                }
            }
        }

        // -----------------------------------------------------------------------
        // Kill
        // -----------------------------------------------------------------------

        /// <summary>
        /// Call from the kill packet handler.
        /// monsterCodeName = CodeName128 of the killed mob.
        /// </summary>
        public static async Task OnMonsterKill(string charName, string monsterCodeName, Proxy proxy)
        {
            var candidates = AchievementLoader.GetByType("kill")
                .Where(a => a.MatchesMob(monsterCodeName));

            await ProcessAchievements(charName, proxy, candidates);
        }

        // -----------------------------------------------------------------------
        // Level
        // -----------------------------------------------------------------------

        /// <summary>
        /// Call when a character levels up. newLevel = the level they just reached.
        /// Level achievements use Count as the target level threshold.
        /// Fires for every level achievement whose Count <= newLevel (in case of multi-level jump).
        /// </summary>
        public static async Task OnLevelUp(string charName, int newLevel, Proxy proxy)
        {
            // For level achievements, Count is the threshold level.
            // We only want to fire ones where Count == newLevel exactly so multi-level jumps
            // don't double-fire — the caller should call this once per level reached.
            var candidates = AchievementLoader.GetByType("level")
                .Where(a => a.Count == newLevel);

            // Level achievements are binary (hit the level = done), so we upsert directly
            // as "complete" by passing amount=Count, goal=Count.
            foreach (var ach in candidates)
            {
                bool newlyCompleted = await DBConnect.IncrementAchievementProgress(
                    charName, ach.Name, ach.Count, ach.Count);

                if (newlyCompleted)
                {
                    Logger.Info(typeof(AchievementService),
                        $"[ACH] {charName} completed level achievement: \"{ach.Name}\"!");
                    PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null,
                        $"ACHIEVEMENT UNLOCKED: {ach.Name} — {ach.Description}");
                }
            }
        }

        // -----------------------------------------------------------------------
        // Gold
        // -----------------------------------------------------------------------

        /// <summary>
        /// Call when a character's gold total changes (pickup, trade, etc.).
        /// currentGold = their new gold total.
        /// Gold achievements use Count as the gold threshold.
        /// These are one-shot: once they've crossed the threshold they complete.
        /// </summary>
        public static async Task OnGoldChanged(string charName, long currentGold, Proxy proxy)
        {
            var candidates = AchievementLoader.GetByType("gold")
                .Where(a => currentGold >= a.Count);

            // Gold achievements are threshold-based, not incremental.
            // We set progress = Count (= goal) to complete them.
            foreach (var ach in candidates)
            {
                bool newlyCompleted = await DBConnect.IncrementAchievementProgress(
                    charName, ach.Name, ach.Count, ach.Count);

                if (newlyCompleted)
                {
                    Logger.Info(typeof(AchievementService),
                        $"[ACH] {charName} completed gold achievement: \"{ach.Name}\"!");
                    PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null,
                        $"ACHIEVEMENT UNLOCKED: {ach.Name} — {ach.Description}");
                }
            }
        }

        // -----------------------------------------------------------------------
        // Death
        // -----------------------------------------------------------------------

        /// <summary>
        /// Call when the character dies.
        /// </summary>
        public static async Task OnDeath(string charName, Proxy proxy)
        {
            var candidates = AchievementLoader.GetByType("death");
            await ProcessAchievements(charName, proxy, candidates);
        }

        /// <summary>
        /// Call when a character uses an item.
        /// itemCodeName = CodeName128 of the used item.
        /// </summary>
        public static async Task OnItemUsed(string charName, string itemCodeName, Proxy proxy)
        {
            var candidates = AchievementLoader.GetByType("itemuse")
                .Where(a => a.MatchesItem(itemCodeName));

            await ProcessAchievements(charName, proxy, candidates);
        }


        /// <summary>
        /// Call when a character picks up a ground item.
        /// itemCodeName = CodeName128 of the picked-up item.
        /// </summary>
        public static async Task OnItemPickup(string charName, string itemCodeName, Proxy proxy)
        {
            var candidates = AchievementLoader.GetByType("itempickup")
                .Where(a => a.MatchesItem(itemCodeName));

            await ProcessAchievements(charName, proxy, candidates);
        }

        /// <summary>
        /// Call each time a character fires an arrow or bolt.
        /// </summary>
        public static async Task OnAmmoFired(string charName, Proxy proxy)
        {
            var candidates = AchievementLoader.GetByType("ammo");
            await ProcessAchievements(charName, proxy, candidates);
        }

        /// <summary>
        /// Call periodically (e.g. every minute from your session tracker).
        /// totalMinutesPlayed = cumulative minutes this character has been online.
        /// Playtime achievements use Count as the minute threshold.
        /// </summary>
        public static async Task OnPlaytimeTick(string charName, long totalMinutesPlayed, Proxy proxy)
        {
            if (totalMinutesPlayed == 0) return;
            var seconds = (long)(proxy.Session!.TotalPlayTime + proxy.Session.AccumulatedPlayTime).TotalSeconds;
            if (seconds % 60 != 0) return;

            var candidates = AchievementLoader.GetByType("playtime")
                .Where(a => totalMinutesPlayed >= a.Count);

            foreach (var ach in candidates)
            {
                bool newlyCompleted = await DBConnect.IncrementAchievementProgress(
                    charName, ach.Name, ach.Count, ach.Count);

                if (newlyCompleted)
                {
                    Logger.Info(typeof(AchievementService),
                        $"[ACH] {charName} completed playtime achievement: \"{ach.Name}\"!");
                    PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null,
                        $"ACHIEVEMENT UNLOCKED: {ach.Name} — {ach.Description}");
                }
            }
        }

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
                    MonsterCodeNames = def.MonsterCodeNames,
                    ItemCodeNames = def.ItemCodeNames,
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

    public class AchievementStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Goal { get; set; }
        public List<string> MonsterCodeNames { get; set; } = new();
        public List<string> ItemCodeNames { get; set; } = new();
        public long Progress { get; set; } = 0;
        public bool Completed { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
    }
}
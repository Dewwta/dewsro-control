using System.Text.RegularExpressions;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.Quest
{
    public class MobDrop
    {
        public string MobName { get; set; } = "";
        public decimal DropChance { get; set; }
    }

    public class QuestMission
    {
        public int    MissionIndex { get; set; }
        public string Type         { get; set; } = ""; // "Kill" | "Gather"
        public string SnCode       { get; set; } = ""; // SN_CON_QNO_CH_GENARAL_3_01

        // Kill fields
        public string? MonsterName  { get; set; }
        public string? MonsterClass { get; set; }
        public int?    KillCount    { get; set; }

        // Gather fields
        public int?         CollectCount { get; set; }
        public string?      ItemName     { get; set; }
        public List<MobDrop>? MobDrops   { get; set; }
    }

    public class QuestFile
    {
        public string           QuestName { get; set; } = "";
        public string           FileName  { get; set; } = "";
        public List<QuestMission> Missions { get; set; } = new();
    }

    public class MobDropUpdate
    {
        public string  MobName    { get; set; } = "";
        public decimal DropChance { get; set; }
    }

    public class QuestMissionUpdate
    {
        public int    MissionIndex { get; set; }
        public string Type         { get; set; } = ""; // "kill" | "gather"

        public int?               KillCount    { get; set; }
        public int?               CollectCount { get; set; }
        public List<MobDropUpdate>? MobDrops   { get; set; }
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    public static class QuestParser
    {
        private static readonly Regex KillRx = new(
            @"LuaSetMissionData(?:_EX)?\(\s*QUESTID\s*,\s*(\d+)\s*,\s*MISSION_TYPE_KILL_MONSTER\s*," +
            @"\s*""([^""]+)""\s*,\s*\d+\s*,\s*""([^""]+)""\s*,\s*\d+\s*,\s*(\w+)\s*,\s*(\d+)",
            RegexOptions.Compiled);

        // LuaSetCollectionItemMissionData[_EX](QUESTID, idx, MISSION_TYPE_GATHER_ITEM_FROM_MONSTER,
        //   "SN_...", N, "NPC_...", 1, COUNT, "ITEM_...", "MOB_A", CHANCE, "MOB_B", CHANCE ...)
        // _EX is optional
        // Groups: (1)idx  (2)snCode  (3)collectCount  (4)itemName  (5)mobTail
        private static readonly Regex GatherRx = new(
            @"LuaSetCollectionItemMissionData(?:_EX)?\(\s*QUESTID\s*,\s*(\d+)\s*,\s*MISSION_TYPE_GATHER_ITEM_FROM_MONSTER\s*," +
            @"\s*""([^""]+)""\s*,\s*\d+\s*,\s*""[^""]+""\s*,\s*\d+\s*,\s*(\d+(?:\.\d+)?)\s*,\s*""([^""]+)""(.*?)\)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Parses "MOB_NAME", CHANCE pairs from the tail of a gather line
        private static readonly Regex MobPairRx = new(
            @"""([^""]+)""\s*,\s*(\d+(?:\.\d+)?)",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses all quest lua files in the Quest subdirectory of questLuaRoot.
        /// Only files that contain at least one KILL or GATHER mission are returned.
        /// </summary>
        public static List<QuestFile> ParseAll(string questLuaRoot)
        {
            string questDir = Path.Combine(questLuaRoot, "Quest");
            if (!Directory.Exists(questDir))
                return new List<QuestFile>();

            var results = new List<QuestFile>();

            foreach (string filePath in Directory.EnumerateFiles(questDir, "*.lua", SearchOption.TopDirectoryOnly))
            {
                var quest = ParseFile(filePath);
                if (quest != null && quest.Missions.Count > 0)
                    results.Add(quest);
            }

            results.Sort((a, b) => string.Compare(a.QuestName, b.QuestName, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        public static QuestFile? ParseFile(string filePath)
        {
            string content;
            try { content = File.ReadAllText(filePath); }
            catch { return null; }

            string fileName  = Path.GetFileName(filePath);
            string questName = ExtractQuestName(content, fileName);

            var missions = new List<QuestMission>();

            foreach (Match m in KillRx.Matches(content))
            {
                missions.Add(new QuestMission
                {
                    MissionIndex = int.Parse(m.Groups[1].Value),
                    Type         = "Kill",
                    SnCode       = m.Groups[2].Value,
                    MonsterName  = m.Groups[3].Value,
                    MonsterClass = m.Groups[4].Value,
                    KillCount    = int.Parse(m.Groups[5].Value)
                });
            }

            foreach (Match m in GatherRx.Matches(content))
            {
                var mobs = new List<MobDrop>();
                foreach (Match mp in MobPairRx.Matches(m.Groups[5].Value))
                {
                    mobs.Add(new MobDrop
                    {
                        MobName    = GameObjectNameResolver.Resolve(mp.Groups[1].Value.Replace("SN_", "")),
                        DropChance = decimal.Parse(mp.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)
                    });
                }

                missions.Add(new QuestMission
                {
                    MissionIndex = int.Parse(m.Groups[1].Value),
                    Type         = "Gather",
                    SnCode       = m.Groups[2].Value,
                    CollectCount = (int)decimal.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                    ItemName     = m.Groups[4].Value,
                    MobDrops     = mobs
                });
            }

            if (missions.Count == 0)
                return null;

            missions.Sort((a, b) => a.MissionIndex.CompareTo(b.MissionIndex));

            return new QuestFile
            {
                QuestName = questName,
                FileName  = fileName,
                Missions  = missions
            };
        }

        /// <summary>
        /// Applies a list of mission updates to the given quest's lua file in-place.
        /// Returns false if the file cannot be found.
        /// </summary>
        public static bool UpdateFile(string questLuaRoot, string questName, List<QuestMissionUpdate> updates)
        {
            string filePath = FindFilePath(questLuaRoot, questName);
            if (filePath == null || !File.Exists(filePath))
                return false;

            string content = File.ReadAllText(filePath);

            foreach (var update in updates)
            {
                if (update.Type == "kill" && update.KillCount.HasValue)
                    content = ApplyKillCount(content, update.MissionIndex, update.KillCount.Value);

                if (update.Type == "gather")
                {
                    if (update.CollectCount.HasValue)
                        content = ApplyGatherCollectCount(content, update.MissionIndex, update.CollectCount.Value);

                    if (update.MobDrops != null)
                    {
                        foreach (var drop in update.MobDrops)
                            content = ApplyMobDropChance(content, update.MissionIndex, drop.MobName, drop.DropChance);
                    }
                }
            }

            File.WriteAllText(filePath, content);
            return true;
        }

        private static string ExtractQuestName(string content, string fileName)
        {
            // Try to extract from: QUESTID = LuaGetQuestID("QUEST_NAME")
            var m = Regex.Match(content, @"LuaGetQuestID\(""([^""]+)""\)");
            if (m.Success) return m.Groups[1].Value;

            // Fallback: strip @SN_ prefix and .lua suffix
            string name = Path.GetFileNameWithoutExtension(fileName);
            if (name.StartsWith("@SN_")) name = name[4..];
            return name;
        }

        private static string FindFilePath(string questLuaRoot, string questName)
        {
            string questDir = Path.Combine(questLuaRoot, "Quest");
            string candidate = Path.Combine(questDir, $"@SN_{questName}.lua");
            if (File.Exists(candidate)) return candidate;

            // Scan for a file whose parsed quest name matches
            foreach (string f in Directory.EnumerateFiles(questDir, "*.lua"))
            {
                string content = File.ReadAllText(f);
                var m = Regex.Match(content, @"LuaGetQuestID\(""([^""]+)""\)");
                if (m.Success && m.Groups[1].Value == questName) return f;
            }

            return null!;
        }

        private static string ApplyKillCount(string content, int missionIndex, int newCount)
        {
            // trailing group captures one or more ", arg" pairs to the closing paren,
            // handling both ", 1, 1)" (_EX) and ", 0)" (non-EX) endings.
            var rx = new Regex(
                $@"(LuaSetMissionData(?:_EX)?\(\s*QUESTID\s*,\s*{missionIndex}\s*,\s*MISSION_TYPE_KILL_MONSTER\s*," +
                @"\s*""[^""]+""\s*,\s*\d+\s*,\s*""[^""]+""\s*,\s*\d+\s*,\s*\w+\s*,\s*)(\d+)(\s*(?:,\s*\w+)+\s*\))");
            return rx.Replace(content, $"${{1}}{newCount}${{3}}");
        }

        private static string ApplyGatherCollectCount(string content, int missionIndex, int newCount)
        {
            var rx = new Regex(
                $@"(LuaSetCollectionItemMissionData(?:_EX)?\(\s*QUESTID\s*,\s*{missionIndex}\s*,\s*MISSION_TYPE_GATHER_ITEM_FROM_MONSTER\s*," +
                @"\s*""[^""]+""\s*,\s*\d+\s*,\s*""[^""]+""\s*,\s*\d+\s*,\s*)(\d+(?:\.\d+)?)(\s*,.*)");
            return rx.Replace(content, $"${{1}}{newCount}${{3}}");
        }

        private static string ApplyMobDropChance(string content, int missionIndex, string mobName, decimal newChance)
        {
            // Find the gather line for this missionIndex, then within it replace the chance for the specific mob
            var lineRx = new Regex(
                $@"(LuaSetCollectionItemMissionData(?:_EX)?\(\s*QUESTID\s*,\s*{missionIndex}\s*,.*?)(\r?\n)",
                RegexOptions.Singleline);

            string escaped = Regex.Escape(mobName);
            return lineRx.Replace(content, m =>
            {
                string line = m.Groups[1].Value;
                string nl   = m.Groups[2].Value;
                var mobRx = new Regex($@"(""{escaped}""\s*,\s*)(\d+(?:\.\d+)?)");
                string updated = mobRx.Replace(line, $"${{1}}{newChance.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                return updated + nl;
            });
        }
    }
}

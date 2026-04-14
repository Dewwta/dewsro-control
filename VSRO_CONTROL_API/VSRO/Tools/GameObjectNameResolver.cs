using CoreLib.Tools.Logging;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public static class GameObjectNameResolver
    {
        private static readonly Dictionary<string, string> _nameMap =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> _skillNameMap =
            new(StringComparer.OrdinalIgnoreCase);

        public static void Load(string textDataFolder)
        {
            _nameMap.Clear();
            _skillNameMap.Clear();

            LoadFile(Path.Combine(textDataFolder, "textdata_equip&skill.txt"));
            LoadFile(Path.Combine(textDataFolder, "textdata_object.txt"));

            Logger.Info(typeof(GameObjectNameResolver),
                $"Loaded {_nameMap.Count} objects, {_skillNameMap.Count} skills.");
        }

        private static void LoadFile(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Warn(typeof(GameObjectNameResolver), $"Missing file: {path}");
                return;
            }

            var lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;

                string key = parts[1].Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                if (key.EndsWith("_TT_DESC", StringComparison.OrdinalIgnoreCase))
                    continue;

                string name = ExtractName(parts);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (key.StartsWith("SN_SKILL_", StringComparison.OrdinalIgnoreCase))
                {

                    // SN_SKILL_XXX
                    if (!_skillNameMap.ContainsKey(key))
                        _skillNameMap[key] = name;

                    // SKILL_XXX (normalized key)
                    var normalizedSkillKey = key.Substring(3); // remove "SN_"
                    if (!_skillNameMap.ContainsKey(normalizedSkillKey))
                        _skillNameMap[normalizedSkillKey] = name;

                    continue;
                }

                // normal objects
                if (!_nameMap.ContainsKey(key))
                    _nameMap[key] = name;
            }
        }

        private static string ExtractName(string[] parts)
        {
            for (int j = parts.Length - 1; j >= 2; j--)
            {
                var val = parts[j]?.Trim();
                if (!string.IsNullOrEmpty(val))
                    return val;
            }
            return "";
        }

        /// <summary>
        /// To fix the armor codenames
        /// </summary>
        /// <param name="codeName"></param>
        /// <returns>Normalized codename</returns>
        private static string NormalizeCode(string codeName)
        {
            if (string.IsNullOrWhiteSpace(codeName))
                return codeName;

            if (codeName.Contains("_CH_M_") || codeName.Contains("_CH_W_") ||
                codeName.Contains("_EU_M_") || codeName.Contains("_EU_W_"))
            {
                codeName = codeName
                    .Replace("_M_", "_")
                    .Replace("_W_", "_");
            }

            return codeName;
        }

        /// <summary>
        /// Resolves ANY CodeName128 (item, mob, skill, etc.)
        /// </summary>
        public static string Resolve(string codeName)
        {
            if (string.IsNullOrWhiteSpace(codeName))
                return codeName;

            if (_skillNameMap.TryGetValue(codeName, out var skillName))
                return skillName;

            // try raw
            if (_nameMap.TryGetValue(codeName, out var name))
                return name;

            // normalize
            var normalized = NormalizeCode(codeName);

            if (_nameMap.TryGetValue(normalized, out name))
                return name;

            if (_skillNameMap.TryGetValue(normalized, out skillName))
                return skillName;

            // SN fallback
            if (_nameMap.TryGetValue("SN_" + normalized, out name))
                return name;

            if (_skillNameMap.TryGetValue("SN_" + normalized, out skillName))
                return skillName;

            return codeName;
        }
    }
}

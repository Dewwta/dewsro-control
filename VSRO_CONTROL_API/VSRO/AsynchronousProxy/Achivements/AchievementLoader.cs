using CoreLib.Tools.Logging;
using System.Xml.Serialization;
using static VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements.AchievementDefinitions;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements
{
    public static class AchievementLoader
    {
        private const string FilePath = "Settings/ach.xml";

        public static AchievementDefinitions? Definitions { get; private set; }
        public static bool IsLoaded { get; private set; } = false;

        public static bool Load()
        {
            try
            {
                using var stream = File.OpenRead(FilePath);
                if (stream.Length == 0)
                    throw new FileNotFoundException("ach.xml is empty.");

                var serializer = new XmlSerializer(typeof(AchievementDefinitions));
                Definitions = (AchievementDefinitions?)serializer.Deserialize(stream);

                IsLoaded = Definitions != null && Definitions.Achievements.Count > 0;

                if (IsLoaded)
                    Logger.Info(typeof(AchievementLoader),
                        $"Loaded {Definitions!.Achievements.Count} achievement definition(s) from ach.xml");
                else
                    Logger.Info(typeof(AchievementLoader), "ach.xml loaded but contained no achievements.");

                return IsLoaded;
            }
            catch (Exception ex)
            {
                Logger.Info(typeof(AchievementLoader),
                    $"Error occurred while loading achievements: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Quick lookup by name. Returns null if not found or not loaded.
        /// </summary>
        public static AchievementDefinition? GetByName(string name)
        {
            if (!IsLoaded || Definitions == null) return null;
            return Definitions.Achievements
                .FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all definitions matching a type (e.g. "kill").
        /// </summary>
        public static List<AchievementDefinition> GetByType(string type)
        {
            if (!IsLoaded || Definitions == null) return new();
            return Definitions.Achievements
                .Where(a => a.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}

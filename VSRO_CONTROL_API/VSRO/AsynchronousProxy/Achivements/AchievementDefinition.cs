using System.Xml.Serialization;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements
{
    [XmlRoot("Achievements")]
    public class AchievementDefinitions
    {
        [XmlElement("Achievement")]
        public List<AchievementDefinition> Achievements { get; set; } = new();

        public class AchievementDefinition
        {
            [XmlAttribute("name")]
            public string Name { get; set; } = string.Empty;

            [XmlAttribute("desc")]
            public string Description { get; set; } = string.Empty;

            [XmlAttribute("type")]
            public string Type { get; set; } = string.Empty;
            // kill | level | gold | death | itemuse | itempickup | ammo | playtime

            [XmlElement("Count")]
            public long Count { get; set; }

            // --- kill ---
            // Empty list = all mobs trigger it
            [XmlElement("MonsterCodeName")]
            public List<string> MonsterCodeNames { get; set; } = new();

            // --- itemuse / itempickup ---
            // Empty list = any item triggers it
            [XmlElement("ItemCodeName")]
            public List<string> ItemCodeNames { get; set; } = new();

            // Helpers
            public bool MatchesMob(string codeName) =>
                MonsterCodeNames.Count == 0 ||
                MonsterCodeNames.Any(m => m.Equals(codeName, StringComparison.OrdinalIgnoreCase));

            public bool MatchesItem(string codeName) =>
                ItemCodeNames.Count == 0 ||
                ItemCodeNames.Any(i => i.Equals(codeName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
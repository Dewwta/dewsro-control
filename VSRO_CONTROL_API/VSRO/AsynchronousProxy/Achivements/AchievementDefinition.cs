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
            public string Type { get; set; } = string.Empty; // "kill", "level", "gold"

            [XmlElement("Count")]
            public long Count { get; set; }

            // Null/omitted = counts toward all
            [XmlElement("MonsterCodeName")]
            public string? MonsterCodeName { get; set; }
        }
    }

}

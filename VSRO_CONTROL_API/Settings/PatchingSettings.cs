using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    public class PatchingSettings
    {
        [XmlElement("GameServerPath")]
        public string? GameServerLocationForPatching { get; set; }

        [XmlElement("MachineManagerPath")]
        public string? MachineManagerLocationForPatching { get; set; }

        [XmlElement("AgentServerPath")]
        public string? AgentServerLocationForPatching { get; set; }
    }
}

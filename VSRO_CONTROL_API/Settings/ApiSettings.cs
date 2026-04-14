using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    [XmlRoot("ApiSettings")]
    public class ApiSettings
    {
        [XmlElement("Network")]
        public NetworkSettings? Network { get; set; }

        [XmlElement("Logs")]
        public LogSettings? Logs { get; set; }

        [XmlElement("Backup")]
        public BackupSettings? Backup { get; set; }

        [XmlElement("Proxy")]
        public ProxySettings? Proxy { get; set; }

        [XmlElement("Patching")]
        public PatchingSettings? Patching { get; set; }

        [XmlElement("Cert")]
        public CertSettings? Cert { get; set; }

        [XmlElement("ServerCfgPath")]
        public string? ServerCfgPath { get; set; }

        [XmlElement("DebugMode")]
        public bool DebugMode { get; set; } = false;
    }
}

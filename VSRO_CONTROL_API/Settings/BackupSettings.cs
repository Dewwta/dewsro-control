using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    public class BackupSettings
    {
        [XmlElement("BackupPath")]
        public string? BackupPath { get; set; }

        [XmlElement("BackupMaxCount")]
        public int BackupMaxCount { get; set; } = 10;

        [XmlElement("BackupDatabases")]
        public string? BackupDatabases { get; set; }
    }
}

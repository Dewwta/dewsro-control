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

        [XmlElement("SecondaryHost")]
        public string? SecondaryHost { get; set; }

        [XmlElement("SecondaryUser")]
        public string? SecondaryUser { get; set; }

        [XmlElement("SecondaryPath")]
        public string? SecondaryPath { get; set; }

        [XmlElement("SecondaryKeyFile")]
        public string? SecondaryKeyFile { get; set; }
    }
}

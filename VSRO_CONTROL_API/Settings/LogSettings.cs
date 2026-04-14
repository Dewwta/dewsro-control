using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    public class LogSettings
    {
        [XmlElement("SaveTimerInHours")]
        public int SaveTimerInHours { get; set; }

        [XmlElement("CleanupTimerInHours")]
        public int CleanupTimerInHours { get; set; }
    }
}

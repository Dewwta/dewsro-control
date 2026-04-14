using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    public class NetworkSettings
    {
        [XmlElement("IP")]
        public string? IP { get; set; }

        [XmlElement("Port")]
        public int Port { get; set; }
    }
}

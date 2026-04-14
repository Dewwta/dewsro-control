using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    public class CertSettings
    {
        [XmlElement("CompileCertPath")]
        public string? CompileCertPath { get; set; }
    }
}

using System.Xml.Serialization;

namespace VSRO_CONTROL_API.Settings
{
    public class ProxySettings
    {
        [XmlElement("IP")]
        public string? IntegratedProxyIP { get; set; } = "192.168.0.11";

        [XmlElement("AgentPort")]
        public int AgentServerPort { get; set; } = 17774;

        [XmlElement("GatewayPort")]
        public int GatewayServerPort { get; set; } = 17779;

        [XmlElement("DownloadPort")]
        public int DownloadServerPort { get; set; } = 17771;

        [XmlElement("SilkPerXHours")]
        public int SilkPerXHours { get; set; } = 0;

        [XmlElement("SilkAmount")]
        public int SilkAmountPerHours { get; set; } = 0;

        [XmlElement("AutoNoticeInterval")]
        public int AutoNoticeInterval { get; set; } = 0;

        [XmlElement("AutoNoticeMessage")]
        public string AutoNoticeMessage { get; set; } = string.Empty;

        [XmlElement("AutoLongestOnlinePlayerInterval")]
        public int AutoLongestPlayerOnlineInterval {  get; set; } = 0;

        [XmlElement("LongestPlayerOnlineMessage")]
        public string LongestPlayerOnlineMessage { get; set; } = "Longest online: {NAME} Time: {TIME}";

        [XmlElement("AfkMessage")]
        public string AFKMessage { get; set; } = "You are AFK";

        [XmlElement("AfkTime")]
        public int AFKTime { get; set; } = 60;

        [XmlElement("BackFromAfkMessage")]
        public string BackFromAfkMessage { get; set; } = "Welcome back! C:";
    }
}

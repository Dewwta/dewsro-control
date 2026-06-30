using Newtonsoft.Json;
using System.Xml.Serialization;


namespace VSRO_CONTROL_API.Settings
{
    public class StartupSettings
    {
        [XmlElement("CertServerPath")]
        [JsonProperty("certServerPath")]
        public string? CertServerPath { get; set; }

        [XmlElement("CertServerArgs")]
        [JsonProperty("certServerArgs")]
        public string? CertServerArgs { get; set; } = "packt.dat";

        [XmlElement("GlobalManagerPath")]
        [JsonProperty("globalManagerPath")]
        public string? GlobalManagerPath { get; set; }

        [XmlElement("DownloadServerPath")]
        [JsonProperty("downloadServerPath")]
        public string? DownloadServerPath { get; set; }

        [XmlElement("MachineManagerPath")]
        [JsonProperty("machineManagerPath")]
        public string? MachineManagerPath { get; set; }

        [XmlElement("GatewayServerPath")]
        [JsonProperty("gatewayServerPath")]
        public string? GatewayServerPath { get; set; }

        [XmlElement("FarmManagerPath")]
        [JsonProperty("farmManagerPath")]
        public string? FarmManagerPath { get; set; }

        [XmlElement("AgentServerPath")]
        [JsonProperty("agentServerPath")]
        public string? AgentServerPath { get; set; }

        [XmlElement("ShardManagerPath")]
        [JsonProperty("shardManagerPath")]
        public string? ShardManagerPath { get; set; }

        [XmlElement("GameServerPath")]
        [JsonProperty("gameServerPath")]
        public string? GameServerPath { get; set; }

        [XmlElement("ProxyPath")]
        [JsonProperty("proxyPath")]
        public string? ProxyPath { get; set; }

        [XmlElement("SMCPath")]
        [JsonProperty("smcPath")]
        public string? SMCPath { get; set; }

        [XmlElement("NodeTypeIniPath")]
        [JsonProperty("nodeTypeIniPath")]
        public string? NodeTypeIniPath { get; set; }

        [XmlElement("SmcUsername")]
        [JsonProperty("smcUsername")]
        public string? SmcUsername { get; set; }

        [XmlElement("SmcPassword")]
        [JsonProperty("smcPassword")]
        public string? SmcPassword { get; set; }

        [XmlElement("SmcMainWindowTitle")]
        [JsonProperty("smcMainWindowTitle")]
        public string? SmcMainWindowTitle { get; set; } = "SMC";

        [XmlElement("ShouldResolvePubIP")]
        [JsonProperty("shouldResolvePubIP")]
        public bool ShouldResolvePubIP { get; set; } = false;

        [XmlElement("QuestLuaRootPath")]
        [JsonProperty("questLuaRootPath")]
        public string? QuestLuaRootPath { get; set; }

        [XmlElement("QuestSctTempPath")]
        [JsonProperty("questSctTempPath")]
        public string? QuestSctTempPath { get; set; }

        [XmlElement("QuestSctDestinationPath")]
        [JsonProperty("questSctDestinationPath")]
        public string? QuestSctDestinationPath { get; set; }

        [XmlElement("QuestTextdataReferencePath")]
        [JsonProperty("questTextdataReferencePath")]
        public string? QuestTextdataReferencePath { get; set; }

        [XmlElement("QuestTextdataOutputPath")]
        [JsonProperty("questTextdataOutputPath")]
        public string? QuestTextdataOutputPath { get; set; }

        [XmlElement("QuestTextdataUpdateFolderPath")]
        [JsonProperty("questTextdataUpdateFolderPath")]
        public string? QuestTextdataUpdateFolderPath { get; set; }
    }
}

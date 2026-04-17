using CoreLib.Tools.Logging;
using Newtonsoft.Json;


namespace VSRO_CONTROL_API.VSRO.Settings
{
    public class StartupSettings
    {
        [JsonProperty("certServerPath")]
        public string? CertServerPath { get; set; }

        [JsonProperty("certServerArgs")]
        public string? CertServerArgs { get; set; } = "packt.dat";

        [JsonProperty("globalManagerPath")]
        public string? GlobalManagerPath { get; set; }

        [JsonProperty("downloadServerPath")]
        public string? DownloadServerPath { get; set; }

        [JsonProperty("machineManagerPath")]
        public string? MachineManagerPath { get; set; }

        [JsonProperty("gatewayServerPath")]
        public string? GatewayServerPath { get; set; }

        [JsonProperty("farmManagerPath")]
        public string? FarmManagerPath { get; set; }

        [JsonProperty("agentServerPath")]
        public string? AgentServerPath { get; set; }

        [JsonProperty("shardManagerPath")]
        public string? ShardManagerPath { get; set; }

        [JsonProperty("gameServerPath")]
        public string? GameServerPath { get; set; }

        [JsonProperty("proxyPath")]
        public string? ProxyPath { get; set; }

        [JsonProperty("smcPath")]
        public string? SMCPath { get; set; }

        [JsonProperty("nodeTypeIniPath")]
        public string? NodeTypeIniPath { get; set; }

        [JsonProperty("smcUsername")]
        public string? SmcUsername { get; set; }

        [JsonProperty("smcPassword")]
        public string? SmcPassword { get; set; }

        [JsonProperty("smcMainWindowTitle")]
        public string? SmcMainWindowTitle { get; set; } = "SMC";

        [JsonProperty("shouldResolvePubIP")]
        public bool? ShouldResolvePubIP { get; set; } = false;

        [JsonProperty("questLuaRootPath")]
        public string? QuestLuaRootPath { get; set; } = null;

        [JsonProperty("questSctTempPath")]
        public string? QuestSctTempPath { get; set; } = null;

        [JsonProperty("questSctDestinationPath")]
        public string? QuestSctDestinationPath { get; set; } = null;

        [JsonProperty("questTextdataReferencePath")]
        public string? QuestTextdataReferencePath { get; set; } = null;

        [JsonProperty("questTextdataOutputPath")]
        public string? QuestTextdataOutputPath { get; set; } = null;

        [JsonProperty("questTextdataUpdateFolderPath")]
        public string? QuestTextdataUpdateFolderPath { get; set; } = null;


        public static async Task<StartupSettings> Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new StartupSettings();
                }

                string jsonContent = File.ReadAllText(filePath);
                var config = JsonConvert.DeserializeObject<StartupSettings>(jsonContent) ?? new StartupSettings();

                return config;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading json configuration {filePath}: {ex.Message}");
            }


            return new StartupSettings();
        }

        public async Task<bool> Save(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting        = Newtonsoft.Json.Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                };

                string jsonContent = JsonConvert.SerializeObject(this, jsonSettings);
                File.WriteAllText(filePath, jsonContent);
                Logger.Info(this, $"Saved startup settings to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving file to {filePath}: {ex.Message}");
                return false;
            }
        }

    }
}

using CoreLib.Tools.Logging;
using Newtonsoft.Json;


namespace VSRO_CONTROL_API.VSRO.Settings
{
    public class StartupSettings
    {
        [JsonProperty("certServerPath")]
        public string? CertServerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\CertModule\\CertModule\\CustomCertificationServer.exe";

        [JsonProperty("certServerArgs")]
        public string? CertServerArgs { get; set; } = "packt.dat";

        [JsonProperty("globalManagerPath")]
        public string? GlobalManagerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\GlobalManager.exe";

        [JsonProperty("downloadServerPath")]
        public string? DownloadServerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\DownloadServer.exe";

        [JsonProperty("machineManagerPath")]
        public string? MachineManagerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\MachineManager.exe";

        [JsonProperty("gatewayServerPath")]
        public string? GatewayServerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\GatewayServer.exe";

        [JsonProperty("farmManagerPath")]
        public string? FarmManagerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\FarmManager.exe";

        [JsonProperty("agentServerPath")]
        public string? AgentServerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\AgentServer.exe";

        [JsonProperty("shardManagerPath")]
        public string? ShardManagerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\SR_ShardManager.exe";

        [JsonProperty("gameServerPath")]
        public string? GameServerPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\DSRO-MAIN\\DSRO-MAIN\\SR_GameServer.exe";

        [JsonProperty("proxyPath")]
        public string? ProxyPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\Filter\\Filter\\VSROProxy.exe";

        [JsonProperty("smcPath")]
        public string? SMCPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\SMC_Independent\\SMC_Independent\\smc_independent.exe";

        [JsonProperty("nodeTypeIniPath")]
        public string? NodeTypeIniPath { get; set; } = "C:\\Users\\VSRO-Host\\Desktop\\VSRO Server\\Server\\Server\\CertModule\\CertModule\\ini\\srNodeType.ini";

        [JsonProperty("smcUsername")]
        public string? SmcUsername { get; set; } = "Dellta";

        [JsonProperty("smcPassword")]
        public string? SmcPassword { get; set; } = "silkroad23"; // Not a real password.

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

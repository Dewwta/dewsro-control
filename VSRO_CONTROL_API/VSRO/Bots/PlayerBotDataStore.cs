using VSRO_CONTROL_API.VSRO.Bot.DTO;
using VSRO_CONTROL_API.VSRO.Bots.DTO;

namespace VSRO_CONTROL_API.VSRO.Bots
{
    public static class PlayerBotDataStore
    {
        private static string GetPath(string charName) =>
            Path.Combine("botconfigs", $"{charName}.json");

        public static void Save(string charName, PlayerBotData config)
        {
            Directory.CreateDirectory("botconfigs");
            File.WriteAllText(GetPath(charName), System.Text.Json.JsonSerializer.Serialize(config));
        }

        public static PlayerBotData? Load(string charName)
        {
            var path = GetPath(charName);
            if (!File.Exists(path)) return null;
            try { return System.Text.Json.JsonSerializer.Deserialize<PlayerBotData>(File.ReadAllText(path)); }
            catch { return null; }
        }
    }
}

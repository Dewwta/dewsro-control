using System.Text;
using System.Text.Json;
using VSRO_CONTROL_API.VSRO.Bots.DTO;

namespace VSRO_CONTROL_API.VSRO
{
    public static class BotApiClient
    {
        private static readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.0.153:5000"),
            Timeout = TimeSpan.FromSeconds(5)   // Fail fast
        };

        public static async Task<BotTrainplace?> GetTrainplace(string botName)
        {
            try
            {
                var res = await _http.GetAsync($"/bot/trainplace/{botName}");
                if (!res.IsSuccessStatusCode) return null;

                var body = await res.Content.ReadAsStringAsync();
                var obj = JsonSerializer.Deserialize<JsonElement>(body);

                return new BotTrainplace
                {
                    X = obj.GetProperty("x").GetInt32(),
                    Y = obj.GetProperty("y").GetInt32(),
                    Z = obj.GetProperty("z").GetInt32(),
                    R = obj.GetProperty("r").GetInt32()
                };
            }
            catch (TaskCanceledException) when (!(_http.Timeout == TimeSpan.Zero)) // Ignore timeout/cancellation
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> SetTrainplace(string botName, int x, int z, int y, int r)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { x, y, z, r });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var res = await _http.PostAsync($"/bot/trainplace/{botName}", content);
                return res.IsSuccessStatusCode;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public class BotStatusResponse
        {
            public bool IsStarting { get; set; }
            public List<string> RunningBots { get; set; } = new();
        }

        public static async Task<BotStatusResponse?> GetStatus()
        {
            try
            {
                var res = await _http.GetAsync("/bot/status");
                if (!res.IsSuccessStatusCode) return null;

                var body = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<BotStatusResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
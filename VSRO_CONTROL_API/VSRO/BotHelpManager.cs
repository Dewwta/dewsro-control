using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.Bots.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO
{
    public static class BotHelpManager
    {
        // Bot name → original trainplace before it was dispatched
        private static readonly ConcurrentDictionary<string, BotTrainplace> _originalPlaces = new();

        // Bot name → when its 1hr session expires
        private static readonly ConcurrentDictionary<string, DateTime> _botExpiry = new();

        // Bot name → which character it's helping
        private static readonly ConcurrentDictionary<string, string> _botAssignments = new();

        // Character name → when their cooldown expires
        private static readonly ConcurrentDictionary<string, DateTime> _playerCooldowns = new();

        // Bot names that are registered as available
        private static readonly List<string> _registeredBots = new();

        public static void RegisterBot(string botName)
        {
            if (!_registeredBots.Contains(botName))
                _registeredBots.Add(botName);
        }

        public static bool IsOnCooldown(string charName, out TimeSpan remaining)
        {
            if (_playerCooldowns.TryGetValue(charName, out var expiry) && DateTime.UtcNow < expiry)
            {
                remaining = expiry - DateTime.UtcNow;
                return true;
            }
            remaining = TimeSpan.Zero;
            return false;
        }

        public static async Task<string?> GetFreeBotAsync()
        {
            try
            {
                var res = await BotApiClient.GetStatus();
                if (res == null) return null;

                foreach (var title in res.RunningBots)
                {
                    // Extract name from "[botPartyOng] SBotP..."
                    var match = System.Text.RegularExpressions.Regex.Match(title, @"^\[(.+?)\]");
                    if (!match.Success) continue;
                    string botName = match.Groups[1].Value;

                    if (!_botAssignments.ContainsKey(botName))
                        return botName;
                }
                return null;
            }
            catch { return null; }
        }

        public static async Task<bool> AssignBot(string botName, string charName,
            int rawX, short z, int rawY, Proxy proxy)
        {
            // Save original trainplace first
            try
            {
                var original = await BotApiClient.GetTrainplace(botName);
                if (original == null) return false;
                _originalPlaces[botName] = original;
            }
            catch { return false; }

            // Set new trainplace to player coords
            bool set = await BotApiClient.SetTrainplace(botName, rawX, (int)z, rawY, 45);
            if (!set) return false;

            _botAssignments[botName] = charName;
            _botExpiry[botName] = DateTime.UtcNow.AddHours(1);
            _playerCooldowns[charName] = DateTime.UtcNow.AddMinutes(90);

            // Schedule auto-release after 1 hour
            _ = Task.Delay(TimeSpan.FromHours(1)).ContinueWith(_ => ReleaseBot(botName, proxy)); // add proxy param

            return true;
        }

        public static async Task ReleaseBot(string botName, Proxy proxy)
        {
            if (_originalPlaces.TryRemove(botName, out var original))
            {
                await BotApiClient.SetTrainplace(botName, original.X, original.Z, original.Y, original.R);
            }
            _botAssignments.TryRemove(botName, out _);
            _botExpiry.TryRemove(botName, out _);
            var botProxy = Overseer.GetProxyByCharacterName(botName);
            if (botProxy != null)
            {
                PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, $"Your bot needs to go help others! It will return to X:{botProxy!.Session!.WorldX} Y:{botProxy!.Session!.WorldY} Z:{botProxy!.Session!.Z} in {RegionResolver.Resolve((short)botProxy!.Session!.RegionId)}");
            }
            else
            {
                PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, $"Your bot needs to go help others! It will return to its original hunting area.");
                Logger.Warn("BotHelpManager:ReleaseBot", $"Bot proxy was null when trying to log release!!");

            }

        }

        public static string? GetBotAssignedTo(string charName)
        {
            return _botAssignments.FirstOrDefault(kv => kv.Value == charName).Key;
        }
    }
}

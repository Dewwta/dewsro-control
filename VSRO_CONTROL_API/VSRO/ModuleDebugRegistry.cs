using CoreLib.Tools.Logging;
using System.Text.Json;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.Bots;

namespace VSRO_CONTROL_API.VSRO
{
    /// <summary>
    /// Registry of runtime-debuggable modules. State is persisted to data/module_debug.json.
    /// To add a module: call Register in the static constructor with its name and SetDebug delegate.
    /// </summary>
    public static class ModuleDebugRegistry
    {
        private sealed record ModuleEntry(Func<bool> GetDebug, Action<bool> SetDebug);

        private static readonly object _lock = new();
        private static readonly Dictionary<string, ModuleEntry> _modules = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string _filePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "data", "module_debug.json");

        static ModuleDebugRegistry()
        {
            Register("PacketParser", () => PacketParser.Debug, PacketParser.SetDebug);
            Register("AttackLoop", () => AttackLoop.Debug, AttackLoop.SetDebug);
            Register("AutoWalker", () => AutoWalker.Debug, AutoWalker.SetDebug);
            Register("TeleportGraph", () => TeleportGraph.Debug, TeleportGraph.SetDebug);
            Register("TownLoop", () => TownLoop.Debug, TownLoop.SetDebug);
            Register("AgentTools", () => AgentTools.Debug, AgentTools.SetDebug);
            Register("PlayerTools", () => PlayerTools.Debug, PlayerTools.SetDebug);
            Register("BotBrain", () => BotBrain.Debug, BotBrain.SetDebug);
            Register("Server", () => Server.Debug, Server.SetDebug);
            Load();
        }

        public static void Register(string name, Func<bool> getDebug, Action<bool> setDebug)
        {
            _modules[name] = new ModuleEntry(getDebug, setDebug);
        }

        public static IReadOnlyList<ModuleDebugStatus> GetAll()
        {
            lock (_lock)
            {
                return _modules
                    .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kvp => new ModuleDebugStatus(kvp.Key, kvp.Value.GetDebug()))
                    .ToList();
            }
        }

        public static bool Set(string name, bool enabled)
        {
            lock (_lock)
            {
                if (!_modules.TryGetValue(name, out var entry))
                    return false;

                entry.SetDebug(enabled);
                Save();
                Logger.Info(typeof(ModuleDebugRegistry), $"Module debug {(enabled ? "enabled" : "disabled")}: {name}");
                return true;
            }
        }

        public static void DisableAll()
        {
            lock (_lock)
            {
                foreach (var entry in _modules.Values)
                    entry.SetDebug(false);
                Save();
                Logger.Info(typeof(ModuleDebugRegistry), "All module debug modes disabled.");
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return;

                var saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(_filePath));
                if (saved == null)
                    return;

                foreach (var kvp in saved)
                {
                    if (_modules.TryGetValue(kvp.Key, out var entry))
                        entry.SetDebug(kvp.Value);
                }

                Logger.Info(typeof(ModuleDebugRegistry), $"Loaded module debug state for {saved.Count} module(s).");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(ModuleDebugRegistry), $"Failed to load module debug state: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var state = _modules.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetDebug());
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(ModuleDebugRegistry), $"Failed to save module debug state: {ex.Message}");
            }
        }

        public record ModuleDebugStatus(string Name, bool DebugEnabled);
    }
}

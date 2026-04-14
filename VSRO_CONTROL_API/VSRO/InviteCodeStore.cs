using CoreLib.Tools.Logging;
using System.Text.Json;

namespace VSRO_CONTROL_API.VSRO
{
    /// <summary>
    /// Thread-safe, file-backed store for one-time invite codes.
    /// Codes survive API restarts. Each code is consumed on successful signup.
    /// </summary>
    public static class InviteCodeStore
    {
        private static readonly object _lock = new();
        private static readonly Dictionary<string, Entry> _codes = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string _filePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "data", "invite_codes.json");

        private sealed record Entry(string Code, DateTime CreatedAt, string Note);

        // ── Startup ───────────────────────────────────────────────────────────

        static InviteCodeStore()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath);
                var entries = JsonSerializer.Deserialize<List<Entry>>(json);
                if (entries == null) return;
                foreach (var e in entries)
                    _codes[e.Code] = e;
                Logger.Info(typeof(InviteCodeStore), $"Loaded {_codes.Count} invite code(s) from disk.");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(InviteCodeStore), $"Failed to load invite codes: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var json = JsonSerializer.Serialize(_codes.Values.ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(InviteCodeStore), $"Failed to save invite codes: {ex.Message}");
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Generates a new cryptographically-random invite code and persists it.</summary>
        public static string Generate(string note = "")
        {
            string code = Guid.NewGuid().ToString("N")[..12].ToUpper();
            lock (_lock)
            {
                _codes[code] = new Entry(code, DateTime.UtcNow, note);
                Save();
            }
            Logger.Info(typeof(InviteCodeStore), $"Invite code generated: {code} ({note})");
            return code;
        }

        /// <summary>
        /// Validates and consumes a code. Removes it from the store and disk if valid.
        /// Returns false if the code does not exist.
        /// </summary>
        public static bool TryConsume(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            lock (_lock)
            {
                if (!_codes.ContainsKey(code)) return false;
                _codes.Remove(code);
                Save();
                Logger.Info(typeof(InviteCodeStore), $"Invite code consumed: {code}");
                return true;
            }
        }

        /// <summary>Removes a code without consuming it (admin revoke). Persists the change.</summary>
        public static bool Revoke(string code)
        {
            lock (_lock)
            {
                if (!_codes.Remove(code)) return false;
                Save();
                Logger.Info(typeof(InviteCodeStore), $"Invite code revoked: {code}");
                return true;
            }
        }

        /// <summary>Returns a snapshot of all active codes.</summary>
        public static IReadOnlyList<InviteCodeEntry> GetAll()
        {
            lock (_lock)
                return _codes.Values
                    .Select(e => new InviteCodeEntry(e.Code, e.CreatedAt, e.Note))
                    .OrderByDescending(e => e.CreatedAt)
                    .ToList();
        }

        public record InviteCodeEntry(string Code, DateTime CreatedAt, string Note);
    }
}

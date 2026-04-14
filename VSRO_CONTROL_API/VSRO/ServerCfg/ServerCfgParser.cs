using CoreLib.Tools.Logging;
using System.Text.RegularExpressions;

namespace VSRO_CONTROL_API.VSRO.ServerCfg
{
    /// <summary>
    /// Parses and writes back a VSRO-style server.cfg file.
    ///
    /// Format:
    ///   BlockName {
    ///       Key   Value        // optional inline comment
    ///       Nested {
    ///           ...            // nested blocks are skipped during key lookup
    ///       }
    ///   }
    /// </summary>
    public class ServerCfgParser
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static ServerCfgParser? Instance { get; private set; }

        public static void Initialize(string filePath)
        {
            Instance = new ServerCfgParser(filePath);
            Logger.Info(typeof(ServerCfgParser), $"Loaded server.cfg from: {filePath}");
        }

        // ── Instance ──────────────────────────────────────────────────────────

        private readonly string _filePath;

        // block (case-insensitive) → key (case-insensitive) → raw value string
        private Dictionary<string, Dictionary<string, string>> _data =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new();

        private ServerCfgParser(string filePath)
        {
            _filePath = filePath;
            Reload();
        }

        // ── Parse ─────────────────────────────────────────────────────────────

        public void Reload()
        {
            if (!File.Exists(_filePath))
            {
                Logger.Warn(typeof(ServerCfgParser), $"server.cfg not found at: {_filePath}");
                return;
            }

            var next = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var blockStack = new Stack<string>();

            foreach (var rawLine in File.ReadAllLines(_filePath))
            {
                var stripped = StripComment(rawLine.Trim());
                if (string.IsNullOrWhiteSpace(stripped)) continue;

                // Block open: "BlockName {"
                if (stripped.EndsWith("{"))
                {
                    var name = stripped[..^1].Trim();
                    blockStack.Push(name);

                    // Only top-level blocks get a dictionary entry
                    if (blockStack.Count == 1)
                        next[name] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    continue;
                }

                // Block close
                if (stripped == "}")
                {
                    if (blockStack.Count > 0) blockStack.Pop();
                    continue;
                }

                // Key-value — only at depth 1 (direct child of a top-level block)
                if (blockStack.Count == 1)
                {
                    var parts = stripped.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                        next[blockStack.Peek()][parts[0]] = parts[1].Trim();
                }
            }

            lock (_lock) { _data = next; }
        }

        // ── Read ──────────────────────────────────────────────────────────────

        /// <summary>Returns the raw string value for a key inside a block, or null.</summary>
        public string? Get(string block, string key)
        {
            lock (_lock)
                return _data.TryGetValue(block, out var b) && b.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>Returns an integer value, or <paramref name="fallback"/> if missing/unparseable.</summary>
        public int GetInt(string block, string key, int fallback = 0)
        {
            var raw = Get(block, key);
            return int.TryParse(raw, out var n) ? n : fallback;
        }

        // ── Write ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Rewrites a single key's value inside the specified block on disk,
        /// preserving every other character of the file (indent, spacing, comments).
        /// Throws <see cref="KeyNotFoundException"/> if the key is not found in the block.
        /// </summary>
        public async Task UpdateValueAsync(string block, string key, string newValue)
        {
            var lines = await File.ReadAllLinesAsync(_filePath);
            bool inTarget   = false;
            int  nestedDepth = 0;
            bool found      = false;

            for (int i = 0; i < lines.Length && !found; i++)
            {
                var stripped = StripComment(lines[i].Trim());
                if (string.IsNullOrWhiteSpace(stripped)) continue;

                if (!inTarget)
                {
                    if (stripped.EndsWith("{"))
                    {
                        var name = stripped[..^1].Trim();
                        if (string.Equals(name, block, StringComparison.OrdinalIgnoreCase))
                            inTarget = true;
                    }
                    continue;
                }

                // Inside target block
                if (stripped.EndsWith("{")) { nestedDepth++; continue; }

                if (stripped == "}")
                {
                    if (nestedDepth == 0) { inTarget = false; }
                    else nestedDepth--;
                    continue;
                }

                // Only replace at depth 0 inside the target block
                if (nestedDepth > 0) continue;

                var parts = stripped.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = RebuildLine(lines[i], key, newValue);
                    found = true;
                }
            }

            if (!found)
                throw new KeyNotFoundException($"Key '{key}' not found in block '{block}' in server.cfg.");

            await File.WriteAllLinesAsync(_filePath, lines);
            Reload();
        }

        /// <summary>
        /// Updates the IP address inside every <c>Certification "ip", port</c> line across all blocks.
        /// Useful for bulk IP migration.
        /// </summary>
        public async Task UpdateAllCertificationIPsAsync(string newIp)
        {
            var text = await File.ReadAllTextAsync(_filePath);

            text = Regex.Replace(
                text,
                @"(Certification\s+"")([^""]+)(""\s*,\s*\d+)",
                m => $"{m.Groups[1].Value}{newIp}{m.Groups[3].Value}",
                RegexOptions.IgnoreCase
            );

            await File.WriteAllTextAsync(_filePath, text);
            Reload();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string StripComment(string s)
        {
            var idx = s.IndexOf("//", StringComparison.Ordinal);
            return idx >= 0 ? s[..idx].Trim() : s;
        }

        /// <summary>
        /// Rebuilds a raw source line, replacing the value portion while preserving
        /// original leading whitespace and any trailing comment.
        /// </summary>
        private static string RebuildLine(string original, string key, string newValue)
        {
            // Leading whitespace
            var indent = original[..(original.Length - original.TrimStart().Length)];

            // Trailing comment (if any), including its leading whitespace on this line
            var noIndent    = original.TrimStart();
            var commentIdx  = noIndent.IndexOf("//", StringComparison.Ordinal);
            var tailComment = commentIdx >= 0
                ? "\t" + noIndent[commentIdx..].TrimEnd()
                : string.Empty;

            return $"{indent}{key}\t\t\t\t{newValue}{tailComment}";
        }
    }
}

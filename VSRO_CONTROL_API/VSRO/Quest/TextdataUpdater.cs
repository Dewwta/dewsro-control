using System.Text;
using System.Text.RegularExpressions;

namespace VSRO_CONTROL_API.VSRO.Quest
{
    public static class TextdataUpdater
    {
        public const string FileName = "textquest_speech&name.txt";

        /// <summary>
        /// Finds the textdata line whose second tab-column matches snCode exactly,
        /// then replaces the count integer in the text column with newCount.
        /// Encoding is auto-detected from the file's BOM (handles UTF-16LE, UTF-8, ANSI).
        /// All tabs are preserved byte-for-byte.
        /// Returns true if the line was found and rewritten.
        /// </summary>
        public static bool UpdateMissionCount(string referencePath, string snCode, int newCount, string? outputPath = null)
        {
            if (!File.Exists(referencePath)) return false;

            Encoding enc;
            string   content;
            using (var sr = new StreamReader(referencePath, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
            {
                content = sr.ReadToEnd();
                enc     = sr.CurrentEncoding;
            }

            var rx = new Regex(
                $@"^(1\t{Regex.Escape(snCode)}\t+(?:[A-Za-z]+[ \t]+)?)(\d+)([ \t][^\r\n]+)",
                RegexOptions.Multiline);

            if (!rx.IsMatch(content)) return false;

            string updated = rx.Replace(content, $"${{1}}{newCount}${{3}}");

            // Write back with the same encoding so the file format is preserved exactly
            using (var sw = new StreamWriter(referencePath, append: false, encoding: enc))
                sw.Write(updated);

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                File.Copy(referencePath, Path.Combine(outputPath, FileName), overwrite: true);
            }

            return true;
        }

        /// <summary>
        /// Parses the quest name block of the textdata file and returns a dictionary
        /// mapping quest code names (SN_ prefix stripped) to their display names.
        /// e.g. "QNO_CH_SMITH_1" -> "Weapon Dealer's Letter"
        /// </summary>
        public static Dictionary<string, string> BuildQuestNameMap(string referencePath, int lineStart = 486, int lineEnd = 1612)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(referencePath)) return map;

            string content;
            using (var sr = new StreamReader(referencePath, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
                content = sr.ReadToEnd();

            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                if (lineNum < lineStart) continue;
                if (lineNum > lineEnd)   break;

                var parts = lines[i].TrimEnd('\r').Split('\t');
                if (parts.Length < 2) continue;

                string snCode      = parts[1].Trim();
                string displayName = parts[parts.Length - 1].Trim();

                if (string.IsNullOrEmpty(snCode) || string.IsNullOrEmpty(displayName)) continue;

                string key = snCode.StartsWith("SN_") ? snCode[3..] : snCode;
                if (!string.IsNullOrEmpty(key))
                    map[key] = displayName;
            }

            return map;
        }

        /// <summary>
        /// Returns the detected encoding name and whether the given snCode appears
        /// anywhere in the file. Used to debug match failures.
        /// </summary>
        public static (string encoding, bool snCodeFound) Diagnose(string referencePath, string snCode)
        {
            if (!File.Exists(referencePath)) return ("file not found", false);

            Encoding enc;
            string   content;
            using (var sr = new StreamReader(referencePath, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
            {
                content = sr.ReadToEnd();
                enc     = sr.CurrentEncoding;
            }

            bool found = content.Contains(snCode, StringComparison.Ordinal);
            return (enc.EncodingName, found);
        }
    }
}

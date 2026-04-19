namespace VSRO_CONTROL_API.VSRO.Patching
{
    public class GameRatePatcher
    {
        private readonly string _gamePath;
        private readonly Dictionary<long, byte[]> _originalBackup = new();

        public GameRatePatcher(string gameServerPath)
        {
            if (!File.Exists(gameServerPath))
                throw new FileNotFoundException("Game server not found", gameServerPath);

            _gamePath = gameServerPath;
        }

        // MAIN ENTRY
        public void ApplyRates(float expRate, float spRate)
        {
            using var fs = new FileStream(_gamePath, FileMode.Open, FileAccess.ReadWrite);

            ApplyFloatPatch(fs, 0x860C80, expRate, "EXP_RATE");
            ApplyFloatPatch(fs, 0x860C84, spRate, "SP_RATE");
        }

        private void ApplyFloatPatch(FileStream fs, long offset, float value, string label)
        {
            byte[] newBytes = BitConverter.GetBytes(value);

            fs.Position = offset;

            byte[] currentBytes = new byte[4];
            fs.Read(currentBytes, 0, 4);

            if (AreEqual(currentBytes, newBytes))
            {
                Console.WriteLine($"[SKIP] {label} already patched.");
                return;
            }

            if (!_originalBackup.ContainsKey(offset))
            {
                _originalBackup[offset] = currentBytes;
            }

            fs.Position = offset;
            fs.Write(newBytes, 0, 4);

            Console.WriteLine($"[OK] Patched {label} → {value}");
        }

        public void Restore()
        {
            using var fs = new FileStream(_gamePath, FileMode.Open, FileAccess.Write);

            foreach (var kv in _originalBackup)
            {
                fs.Position = kv.Key;
                fs.Write(kv.Value, 0, kv.Value.Length);
            }

            Console.WriteLine("[OK] Restored original values.");
        }

        private bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;

            return true;
        }
    }
}

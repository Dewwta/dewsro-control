using System.Text;

namespace VSRO_CONTROL_API.VSRO.Patching
{

    /// <summary>
    /// Binary patcher for vSRO 1.188 AgentServer.exe.
    /// Patches the IP address the module binds to.
    /// </summary>
    public class AgentServerPatcher
    {
        public record PatchResult(bool Success, string Message);
        private readonly string _exePath;
        private readonly string _backupDir;

        public AgentServerPatcher(string exePath)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"AgentServer.exe not found at: {exePath}");

            _exePath = exePath;
            _backupDir = Path.Combine(Path.GetDirectoryName(exePath)!, "patch_backups");
        }

        /// <summary>
        /// Patches the IP address the AgentServer binds to.
        /// Writes ASM redirects at two offsets and the IP string to a 35-byte buffer.
        /// </summary>
        public PatchResult SpoofIP(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return new PatchResult(false, "IP address cannot be empty.");

            byte[] ipBytes = Encoding.ASCII.GetBytes(ipAddress);
            if (ipBytes.Length > 35)
                return new PatchResult(false, "IP address string is too long (max 35 characters).");

            // Backup
            try
            {
                Directory.CreateDirectory(_backupDir);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(_exePath, Path.Combine(_backupDir, $"AgentServer_{timestamp}.exe"), overwrite: false);
            }
            catch (Exception ex)
            {
                return new PatchResult(false, $"Backup failed: {ex.Message}. Aborting patch.");
            }

            // Patch
            try
            {
                using var fs = new FileStream(_exePath, FileMode.Open, FileAccess.ReadWrite);
                using var writer = new BinaryWriter(fs);

                // ASM redirects
                byte[] asmRedirect = { 0xFA, 0x27 };
                writer.Seek(213882, SeekOrigin.Begin);
                writer.Write(asmRedirect, 0, 2);
                writer.Seek(213966, SeekOrigin.Begin);
                writer.Write(asmRedirect, 0, 2);

                // Clear 35-byte buffer and write new IP
                byte[] ipBuffer = new byte[35];
                Array.Copy(ipBytes, ipBuffer, ipBytes.Length);
                writer.Seek(796666, SeekOrigin.Begin);
                writer.Write(ipBuffer, 0, 35);

                writer.Flush();
                return new PatchResult(true, $"AgentServer.exe IP patched to: {ipAddress}");
            }
            catch (IOException ex)
            {
                return new PatchResult(false, $"File access error: {ex.Message}. Is AgentServer running?");
            }
            catch (Exception ex)
            {
                return new PatchResult(false, $"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the currently patched IP from the executable.
        /// </summary>
        public string GetCurrentIP()
        {
            using var fs = new FileStream(_exePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            fs.Seek(796666, SeekOrigin.Begin);
            byte[] ipBytes = reader.ReadBytes(35);
            int nullIndex = Array.IndexOf(ipBytes, (byte)0);
            return nullIndex >= 0
                ? Encoding.ASCII.GetString(ipBytes, 0, nullIndex)
                : Encoding.ASCII.GetString(ipBytes);
        }
    }
}

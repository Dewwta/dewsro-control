using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.DTO.Patching;
using VSRO_CONTROL_API.VSRO.Patching;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class PatchController : ControllerBase
    {

        private static IActionResult? ServerRunningGuard()
        {
            var status = Overseer.GetServerStatus();
            if (status.IsRunning)
                return new ConflictObjectResult(new
                {
                    message = $"Cannot patch while the server is running. " +
                              $"{status.ModulesOpened} module(s) are still active. " +
                              "Shut down all modules completely before patching."
                });
            return null;
        }

        [HttpPost("game-server")]
        public IActionResult PatchGameServerModule([FromBody] GameServerPatchRequest req)
        {
            var guard = ServerRunningGuard();
            if (guard != null) return guard;

            try
            {
                string? exePath = SettingsLoader.Settings?.Patching?.GameServerLocationForPatching;
                if (string.IsNullOrWhiteSpace(exePath))
                    return BadRequest(new { message = "GameServerLocationForPatching is not configured in settings." });

                var patcher = new GameServerPatcher(exePath);

                if (req.MaxLevel.HasValue)
                    patcher.SetMaxLevel((byte)req.MaxLevel.Value);

                if (req.MasteryLimit.HasValue)
                    patcher.SetMasteryLimit((short)req.MasteryLimit.Value);

                if (req.FixRates)
                    patcher.FixRates();

                if (req.DisableDumpFiles)
                    patcher.DisableDumpFiles();

                if (req.DisableGreenBook)
                    patcher.DisableGreenBook();

                if (!string.IsNullOrWhiteSpace(req.IpToSet))
                    patcher.SpoofIP(req.IpToSet);

                if (req.ObjectLimitToSet.HasValue)
                    patcher.SetObjectLimit(req.ObjectLimitToSet.Value);

                var result = patcher.Apply();

                if (!result.Success)
                    return BadRequest(new { message = result.Message, applied = result.AppliedPatches });

                Logger.Info(this, result.Message);
                return Ok(new { message = result.Message, applied = result.AppliedPatches });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                string msg = $"Error occurred while patching: {ex.Message}";
                Logger.Error(this, msg);
                return StatusCode(500, new { message = msg });
            }
        }

        [HttpGet("game-server")]
        public IActionResult GetCurrentGameServerValues()
        {
            try
            {
                string? exePath = SettingsLoader.Settings?.Patching?.GameServerLocationForPatching;
                if (string.IsNullOrWhiteSpace(exePath))
                    return BadRequest(new { message = "GameServerLocationForPatching is not configured in settings." });

                if (!System.IO.File.Exists(exePath))
                    return NotFound(new { message = $"SR_GameServer.exe not found at: {exePath}" });

                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);

                // Max Level (offset 877598, 1 byte)
                fs.Seek(877598, SeekOrigin.Begin);
                byte maxLevel = reader.ReadByte();

                // Mastery Limit (offset 1689063, 2 bytes)
                fs.Seek(1689063, SeekOrigin.Begin);
                short masteryLimit = reader.ReadInt16();

                // Fix Rates (offset 160078, check if byte is 0x42)
                fs.Seek(160078, SeekOrigin.Begin);
                bool ratesFixed = reader.ReadByte() == 0x42;

                // Disable Dumps (offset 5652576, check if first byte is 0xE9)
                fs.Seek(5652576, SeekOrigin.Begin);
                bool dumpsDisabled = reader.ReadByte() == 0xE9;

                // Disable Green Book (offset 82747, check if first byte is 0x90)
                fs.Seek(82747, SeekOrigin.Begin);
                bool greenBookDisabled = reader.ReadByte() == 0x90;

                // Spoof IP (offset 7179808, read 32 bytes as string)
                fs.Seek(7179808, SeekOrigin.Begin);
                byte[] ipBytes = reader.ReadBytes(32);
                int nullIndex = Array.IndexOf(ipBytes, (byte)0);
                string spoofedIP = nullIndex >= 0
                    ? Encoding.ASCII.GetString(ipBytes, 0, nullIndex)
                    : Encoding.ASCII.GetString(ipBytes);

                // Object Limit — need VA-to-file conversion
                int objectLimit = 50000; // default
                try
                {
                    long vaAdjustment = CalculateVAToFileOffset(exePath);
                    long countOffset = 0x0054D60A - vaAdjustment;
                    fs.Seek(countOffset, SeekOrigin.Begin);
                    objectLimit = (int)reader.ReadUInt32();
                }
                catch
                {
                    // If PE parsing fails, leave as default
                }

                return Ok(new
                {
                    maxLevel = (int)maxLevel,
                    masteryLimit = (int)masteryLimit,
                    ratesFixed,
                    dumpsDisabled,
                    greenBookDisabled,
                    spoofedIP,
                    objectLimit
                });
            }
            catch (Exception ex)
            {
                string msg = $"Error occurred while reading current values: {ex.Message}";
                Logger.Error(this, msg);
                return StatusCode(500, new { message = msg });
            }
        }

        [HttpPost("machine-manager/spoof-ip")]
        public IActionResult PatchMachineManagerIP([FromBody] IpPatchRequest req)
        {
            var guard = ServerRunningGuard();
            if (guard != null) return guard;

            string? exePath = SettingsLoader.Settings?.Patching?.MachineManagerLocationForPatching;
            if (string.IsNullOrWhiteSpace(exePath))
                return BadRequest(new { message = "MachineManagerLocationForPatching is not configured." });

            var result = new MachineManagerPatcher(exePath).SpoofIP(req.IpAddress);
            return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
        }

        [HttpPost("agent-server/spoof-ip")]
        public IActionResult PatchAgentServerIP([FromBody] IpPatchRequest req)
        {
            var guard = ServerRunningGuard();
            if (guard != null) return guard;

            string? exePath = SettingsLoader.Settings?.Patching?.AgentServerLocationForPatching;
            if (string.IsNullOrWhiteSpace(exePath))
                return BadRequest(new { message = "AgentServerLocationForPatching is not configured." });

            var result = new AgentServerPatcher(exePath).SpoofIP(req.IpAddress);
            return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
        }

        // ── Node INI (Cert Server) ────────────────────────────────────────────────

        private NodeIniHelper? GetNodeIniHelper(out IActionResult? error)
        {
            error = null;
            var iniPath = Overseer.GetSettings()?.NodeTypeIniPath;
            if (string.IsNullOrWhiteSpace(iniPath))
            {
                error = BadRequest(new { message = "nodeTypeIniPath is not configured in startup settings." });
                return null;
            }
            var root = Path.GetDirectoryName(iniPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                error = BadRequest(new { message = "Could not derive INI directory from nodeTypeIniPath." });
                return null;
            }
            return new NodeIniHelper(root);
        }

        [HttpGet("node-inis")]
        public async Task<IActionResult> GetNodeIniValues()
        {
            var helper = GetNodeIniHelper(out var err);
            if (err != null) return err;

            var values = await helper!.ReadAllInis();
            if (values == null)
                return StatusCode(500, new { message = "Failed to read INI files. Check that nodeTypeIniPath is correct." });

            return Ok(new
            {
                wip          = values.Wip,
                nip          = values.Nip,
                shardName    = values.ShardName,
                accountQuery = values.AccountQuery,
                shardQuery   = values.ShardQuery,
                logQuery     = values.LogQuery,
            });
        }

        public record NodeIniPatchRequest(
            string? Ip,
            string? ShardName,
            string? AccountQuery,
            string? ShardQuery,
            string? LogQuery);

        [HttpPost("node-inis")]
        public async Task<IActionResult> PatchNodeInis([FromBody] NodeIniPatchRequest req)
        {
            var guard = ServerRunningGuard();
            if (guard != null) return guard;

            var helper = GetNodeIniHelper(out var err);
            if (err != null) return err;

            bool ok = await helper!.PatchAllInis(req.Ip, req.ShardName, req.AccountQuery, req.ShardQuery, req.LogQuery);

            if (!ok)
                return BadRequest(new { message = "Patching failed or nothing to patch. Check the logs." });

            string? compilePath = SettingsLoader.Settings?.Cert?.CompileCertPath;
            string compileMsg = "";
            if (!string.IsNullOrWhiteSpace(compilePath))
            {
                bool compiled = await helper.CompileCert(compilePath);
                compileMsg = compiled ? " Cert compiled successfully." : " Cert compile step failed — check logs.";
            }

            return Ok(new { message = $"INI files patched successfully.{compileMsg}" });
        }

        // Reuse the PE parsing logic from GameServerPatcher
        private static long CalculateVAToFileOffset(string exePath)
        {
            using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            fs.Seek(0x3C, SeekOrigin.Begin);
            int peHeaderOffset = reader.ReadInt32();

            fs.Seek(peHeaderOffset + 4, SeekOrigin.Begin);
            reader.ReadUInt16(); // machine
            ushort numberOfSections = reader.ReadUInt16();
            reader.ReadBytes(12);
            ushort sizeOfOptionalHeader = reader.ReadUInt16();
            reader.ReadUInt16(); // characteristics

            long optionalHeaderStart = fs.Position;
            ushort magic = reader.ReadUInt16();

            uint imageBase;
            if (magic == 0x10B)
            {
                fs.Seek(optionalHeaderStart + 28, SeekOrigin.Begin);
                imageBase = reader.ReadUInt32();
            }
            else
            {
                fs.Seek(optionalHeaderStart + 24, SeekOrigin.Begin);
                imageBase = (uint)reader.ReadUInt64();
            }

            fs.Seek(optionalHeaderStart + sizeOfOptionalHeader, SeekOrigin.Begin);

            for (int i = 0; i < numberOfSections; i++)
            {
                byte[] nameBytes = reader.ReadBytes(8);
                string sectionName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                reader.ReadUInt32(); // virtualSize
                uint virtualAddress = reader.ReadUInt32();
                reader.ReadUInt32(); // rawDataSize
                uint rawDataPointer = reader.ReadUInt32();
                reader.ReadBytes(16);

                if (sectionName == ".text" || i == 0)
                    return (long)imageBase + (long)virtualAddress - (long)rawDataPointer;
            }

            throw new Exception("Could not find .text section in PE headers.");
        }
    }
}

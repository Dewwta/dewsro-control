using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.Quest;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class QuestController : ControllerBase
    {
        // GET api/quest?page=1&search=
        [HttpGet]
        public IActionResult GetQuests([FromQuery] int page = 1, [FromQuery] string? search = null)
        {
            var settings = Overseer.GetSettings();
            if (string.IsNullOrWhiteSpace(settings?.QuestLuaRootPath))
                return BadRequest(new { message = "Quest Lua root path is not configured in startup settings." });

            if (!Directory.Exists(Path.Combine(settings.QuestLuaRootPath, "Quest")))
                return NotFound(new { message = "Quest folder not found at the configured Lua root path." });

            var all = QuestParser.ParseAll(settings.QuestLuaRootPath);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                // Build display-name map if the textdata file is configured, so users can
                // search by either code name ("QNO_CH_SMITH_1") or display name ("Weapon Dealer").
                Dictionary<string, string>? nameMap = null;
                string? refPath = settings.QuestTextdataReferencePath;
                if (!string.IsNullOrWhiteSpace(refPath) && System.IO.File.Exists(refPath))
                    nameMap = TextdataUpdater.BuildQuestNameMap(refPath);

                all = all.Where(q =>
                    q.QuestName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (nameMap != null &&
                     nameMap.TryGetValue(q.QuestName, out var displayName) &&
                     displayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            const int pageSize = 30;
            int total = all.Count;
            int totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new
            {
                quests     = paged,
                total,
                page,
                pageSize,
                totalPages
            });
        }
        // GET api/quest/textdata/diagnose?snCode=SN_CON_QNO_EU_SOLDIER_EA2_1
        [AllowAnonymous]
        [HttpGet("textdata/diagnose")]
        public IActionResult DiagnoseTextdata([FromQuery] string snCode)
        {
            string? path = Overseer.GetSettings()?.QuestTextdataReferencePath;
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(new { message = "QuestTextdataReferencePath not configured." });

            var (encoding, found) = TextdataUpdater.Diagnose(path, snCode);
            return Ok(new { encoding, snCodeFound = found, path });
        }

        // PUT api/quest/{questName}
        [HttpPut("{questName}")]
        public IActionResult UpdateQuest(string questName, [FromBody] QuestUpdateBody body)
        {
            var settings = Overseer.GetSettings();
            if (string.IsNullOrWhiteSpace(settings?.QuestLuaRootPath))
                return BadRequest(new { message = "Quest Lua root path is not configured." });

            if (body?.Missions == null || body.Missions.Count == 0)
                return BadRequest(new { message = "No missions provided." });

            // Parse BEFORE updating so we have the exact SN codes from the Lua for textdata sync.
            // The SN code is embedded directly in each LuaSetMissionData call and is the authoritative
            // textdata key — do NOT derive it from the quest name, as single-mission quests omit the
            // index suffix (e.g. "SN_CON_QNO_CH_SMITH_2" not "SN_CON_QNO_CH_SMITH_2_01").
            var parsed = QuestParser.ParseFile(
                System.IO.Path.Combine(settings.QuestLuaRootPath, "Quest", $"@SN_{questName}.lua"));
            if (parsed == null)
            {
                var all = QuestParser.ParseAll(settings.QuestLuaRootPath);
                parsed = all.FirstOrDefault(q => q.QuestName == questName);
            }

            bool ok = QuestParser.UpdateFile(settings.QuestLuaRootPath, questName, body.Missions);
            if (!ok)
                return NotFound(new { message = $"Quest file for '{questName}' not found." });

            // Update textdata counts using the SN code extracted directly from the Lua
            string textdataStatus = "";
            string? refPath = settings.QuestTextdataReferencePath;

            if (string.IsNullOrWhiteSpace(refPath))
            {
                textdataStatus = " (textdata skipped: QuestTextdataReferencePath not configured)";
            }
            else if (!System.IO.File.Exists(refPath))
            {
                textdataStatus = $" (textdata skipped: file not found at {refPath})";
            }
            else if (parsed != null)
            {
                int updated = 0;
                int missed  = 0;
                foreach (var update in body.Missions)
                {
                    var mission = parsed.Missions.FirstOrDefault(m => m.MissionIndex == update.MissionIndex);
                    if (mission == null || string.IsNullOrEmpty(mission.SnCode)) continue;

                    int? newCount = update.Type == "kill"   ? update.KillCount
                                  : update.Type == "gather" ? update.CollectCount
                                  : null;
                    if (!newCount.HasValue) continue;

                    bool ok1 = TextdataUpdater.UpdateMissionCount(refPath, mission.SnCode, newCount.Value, settings.QuestTextdataOutputPath);
                    if (ok1) updated++; else missed++;
                }
                textdataStatus = updated > 0
                    ? $" | textdata: {updated} line(s) updated{(missed > 0 ? $", {missed} SN code(s) not found" : "")}"
                    : " (textdata: no matching SN codes found in reference file)";

                if (updated > 0 && !string.IsNullOrWhiteSpace(settings.QuestTextdataUpdateFolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(settings.QuestTextdataUpdateFolderPath);
                        string dest = System.IO.Path.Combine(settings.QuestTextdataUpdateFolderPath, TextdataUpdater.FileName);
                        System.IO.File.Copy(refPath, dest, overwrite: true);
                        textdataStatus += " | synced to update folder";
                        Logger.Info(typeof(QuestController), $"{TextdataUpdater.FileName} synced to update folder: {dest}");
                    }
                    catch (Exception ex)
                    {
                        textdataStatus += $" | update folder sync failed: {ex.Message}";
                        Logger.Error(typeof(QuestController), $"Failed to sync textdata to update folder: {ex.Message}");
                    }
                }
            }

            Logger.Info(typeof(QuestController), $"Quest saved: {questName}{textdataStatus}");
            return Ok(new { message = $"Quest '{questName}' saved.{textdataStatus}" });
        }

        // GET api/quest/textdata/download
        [HttpGet("textdata/download")]
        public IActionResult DownloadTextdata()
        {
            string? path = Overseer.GetSettings()?.QuestTextdataReferencePath;
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(new { message = "QuestTextdataReferencePath is not configured in startup settings." });

            if (!System.IO.File.Exists(path))
                return NotFound(new { message = $"textquest_speech&name.txt not found at: {path}" });

            byte[] bytes = System.IO.File.ReadAllBytes(path);
            return File(bytes, "text/plain", FileName);
        }

        private const string FileName = "textquest_speech&name.txt";

        // POST api/quest/compile
        [HttpPost("compile")]
        public IActionResult Compile()
        {
            var settings = Overseer.GetSettings();
            if (string.IsNullOrWhiteSpace(settings?.QuestLuaRootPath))
                return BadRequest(new { message = "Quest Lua root path is not configured." });

            string batPath = Path.Combine(settings.QuestLuaRootPath, "make_quest.bat");
            if (!System.IO.File.Exists(batPath))
                return NotFound(new { message = $"make_quest.bat not found at: {batPath}" });

            Logger.Info(typeof(QuestController), $"Running make_quest.bat at: {batPath}");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName         = "cmd.exe",
                    Arguments        = $"/c \"{batPath}\"",
                    WorkingDirectory = settings.QuestLuaRootPath,
                    UseShellExecute  = false,
                    CreateNoWindow   = true
                };
                using var proc = Process.Start(psi)
                    ?? throw new Exception("cmd.exe process could not be started.");
                proc.WaitForExit(120_000);
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(QuestController), $"make_quest.bat failed: {ex.Message}");
                return StatusCode(500, new { message = $"Failed to run bat: {ex.Message}" });
            }

            // Poll for Quest.sct — helper.exe may flush to disk slightly after cmd.exe exits
            string outputSct = Path.Combine(settings.QuestLuaRootPath, "Quest.sct");
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!System.IO.File.Exists(outputSct) && DateTime.UtcNow < deadline)
                Thread.Sleep(500);

            if (!System.IO.File.Exists(outputSct))
            {
                Logger.Error(typeof(QuestController), $"Quest.sct not found after compilation at: {outputSct}");
                return StatusCode(500, new { message = $"Compilation timed out waiting for Quest.sct at: {outputSct}" });
            }

            if (!string.IsNullOrWhiteSpace(settings.QuestSctTempPath))
            {
                Directory.CreateDirectory(settings.QuestSctTempPath);
                string dest = Path.Combine(settings.QuestSctTempPath, "Quest.sct");
                System.IO.File.Copy(outputSct, dest, overwrite: true);
                System.IO.File.Delete(outputSct);
                Logger.Info(typeof(QuestController), $"Quest.sct compiled and staged at: {dest}");
                return Ok(new { message = $"Compiled, staged at: {dest}, and removed from Lua root." });
            }

            Logger.Info(typeof(QuestController), "Quest.sct compiled (no temp path configured, left in Lua root).");
            return Ok(new { message = "Compiled. Quest.sct is in the Lua root (no temp path configured)." });
        }
    }

    public class QuestUpdateBody
    {
        public List<QuestMissionUpdate> Missions { get; set; } = new();
    }
}

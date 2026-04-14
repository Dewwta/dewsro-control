using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO.Backup;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/backup")]
    public class BackupController : ControllerBase
    {
        // POST api/backup/run — admin only, triggers an immediate backup of all configured databases
        [RequireAdmin]
        [HttpPost("run")]
        public async Task<IActionResult> RunBackup()
        {
            var svc = DatabaseBackupService.Instance;
            if (svc == null)
                return StatusCode(503, new { message = "Backup service is not available." });

            var (success, message) = await svc.RunBackupsAsync();
            return success
                ? Ok(new { message })
                : StatusCode(500, new { message });
        }

        // GET api/backup/status — admin only, returns service state + file listing
        [RequireAdmin]
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var svc = DatabaseBackupService.Instance;
            if (svc == null)
                return StatusCode(503, new { message = "Backup service is not available." });

            return Ok(svc.GetStatus());
        }
    }
}

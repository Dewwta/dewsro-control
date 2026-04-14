using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class TextdataController : ControllerBase
    {
        private static readonly string TextdataPath = Path.Combine(Environment.CurrentDirectory, "textdata");

        // POST api/textdata/generate
        [HttpPost("generate")]
        public IActionResult Generate()
        {
            if (DBConnect.TextdataGenerationRunning)
                return Conflict(new { message = "Textdata generation is already in progress." });

            _ = Task.Run(async () => await DBConnect.DumpAllData());

            return Accepted(new { message = "Textdata generation started." });
        }

        // GET api/textdata/status
        [HttpGet("status")]
        public IActionResult Status()
        {
            return Ok(new { running = DBConnect.TextdataGenerationRunning, progress = DBConnect.TextdataGenerationProgress });
        }

        // GET api/textdata/download
        [HttpGet("download")]
        [AllowAnonymous]
        public IActionResult Download()
        {
            if (DBConnect.TextdataGenerationRunning)
                return Conflict(new { message = "Generation is still in progress. Please wait." });

            if (!Directory.Exists(TextdataPath) || !Directory.EnumerateFiles(TextdataPath).Any())
                return NotFound(new { message = "No textdata files found. Run generation first." });

            var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in Directory.GetFiles(TextdataPath))
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
            memoryStream.Position = 0;
            	
            string filename = $"textdata_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
            return File(memoryStream, "application/zip", filename);
        }
    }
}

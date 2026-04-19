using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VSRO_CONTROL_API.Attributes;
using CoreLib.Tools.Logging;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private static readonly string ClientsDir = Path.Combine(Environment.CurrentDirectory, "clients");
        private static readonly string ManifestPath = Path.Combine(ClientsDir, "manifest.json");
        private const int MaxClients = 5;

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string version)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            if (string.IsNullOrWhiteSpace(version))
                return BadRequest(new { message = "Version string is required." });

            var safeVersion = version.Trim()
                .Replace(" ", "").Replace("/", "").Replace("\\", "")
                .Replace("..", "").Replace(":", "");

            Directory.CreateDirectory(ClientsDir);

            var fileName = $"VSRO_Client_v{safeVersion}.zip";
            var filePath = Path.Combine(ClientsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await file.CopyToAsync(stream);

            var manifest = LoadManifest();

            // Replace any existing entry for the same version
            manifest.RemoveAll(e => e.Version == safeVersion);

            manifest.Add(new ClientEntry
            {
                Version    = safeVersion,
                FileName   = fileName,
                UploadedAt = DateTime.UtcNow,
                SizeBytes  = file.Length
            });

            // Keep newest MaxClients; delete files for anything that falls off
            manifest = manifest.OrderByDescending(e => e.UploadedAt).ToList();

            while (manifest.Count > MaxClients)
            {
                var oldest = manifest[^1];
                var oldPath = Path.Combine(ClientsDir, oldest.FileName);
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
                manifest.RemoveAt(manifest.Count - 1);
            }

            SaveManifest(manifest);

            return Ok(new { message = $"Client v{safeVersion} uploaded successfully.", fileName });
        }

        [HttpGet("latest")]
        public IActionResult GetLatest()
        {
            var manifest = LoadManifest();
            var latest   = manifest.OrderByDescending(e => e.UploadedAt).FirstOrDefault();

            if (latest == null)
                return NotFound(new { message = "No client available." });

            return Ok(BuildEntry(latest));
        }

        [RequireAdmin]
        [HttpGet("list")]
        public IActionResult List()
        {
            var manifest = LoadManifest().OrderByDescending(e => e.UploadedAt);
            return Ok(manifest.Select(BuildEntry));
        }

        [RequireAdmin]
        [HttpDelete("{fileName}")]
        public IActionResult Delete(string fileName)
        {
            var safe     = Path.GetFileName(fileName); // prevents path traversal
            var manifest = LoadManifest();
            var entry    = manifest.FirstOrDefault(e => e.FileName == safe);

            if (entry == null)
                return NotFound(new { message = "Entry not found in manifest." });

            var filePath = Path.Combine(ClientsDir, safe);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            manifest.Remove(entry);
            SaveManifest(manifest);

            return Ok(new { message = $"Client v{entry.Version} deleted." });
        }

        [RequireAuth]
        [HttpGet("download/{fileName}")]
        public IActionResult Download(string fileName)
        {
            var safe     = Path.GetFileName(fileName);
            var filePath = Path.Combine(ClientsDir, safe);

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "File not found." });

            return PhysicalFile(filePath, "application/zip", safe, enableRangeProcessing: true);
        }

        private object BuildEntry(ClientEntry e) => new
        {
            version     = e.Version,
            fileName    = e.FileName,
            uploadedAt  = e.UploadedAt,
            sizeBytes   = e.SizeBytes,
            downloadUrl = $"{Request.Scheme}://{Request.Host}/api/client/download/{e.FileName}"
        };

        private List<ClientEntry> LoadManifest()
        {
            if (!System.IO.File.Exists(ManifestPath)) return [];
            try
            {
                var json = System.IO.File.ReadAllText(ManifestPath);
                return JsonSerializer.Deserialize<List<ClientEntry>>(json) ?? [];
            }
            catch { return []; }
        }

        private void SaveManifest(List<ClientEntry> manifest)
        {
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(ManifestPath, json);
        }

        private sealed class ClientEntry
        {
            public string   Version    { get; set; } = "";
            public string   FileName   { get; set; } = "";
            public DateTime UploadedAt { get; set; }
            public long     SizeBytes  { get; set; }
        }
    }
}

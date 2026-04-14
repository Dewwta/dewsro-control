using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class LogsController : ControllerBase
    {
        // GET api/logs?since=N
        // Returns all entries from index N onwards, plus the current total count.
        // If 'since' exceeds the total (e.g. after a log flush cleared history),
        // the client should reset its index and re-fetch from 0.
        [HttpGet]
        public IActionResult GetLogs([FromQuery] int since = 0)
        {
            // Snapshot to avoid holding the internal list reference across an async gap
            var all = Logger.GetAllLogs().ToList();

            int total = all.Count;
            int from  = (since > total) ? 0 : Math.Max(0, since);

            var entries = all.Skip(from).ToList();

            return Ok(new { total, entries });
        }

        // DELETE api/logs — clears in-memory log history
        [HttpDelete]
        public IActionResult ClearLogs()
        {
            Logger.ClearHistory();
            return Ok(new { message = "Log history cleared." });
        }
    }
}

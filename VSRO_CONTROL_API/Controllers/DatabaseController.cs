using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class DatabaseController : ControllerBase
    {
        public record QueryRequest(string Sql);

        // ── Saved queries storage ─────────────────────────────────────────────

        private static readonly string _savedQueriesPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs", "saved-queries.json");

        private static readonly JsonSerializerOptions _jsonOpts =
            new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public class SavedQuery
        {
            public string Id        { get; set; } = Guid.NewGuid().ToString();
            public string Name      { get; set; } = "";
            public string Sql       { get; set; } = "";
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }

        public record SaveQueryRequest(string Name, string Sql);

        private static List<SavedQuery> LoadSavedQueries()
        {
            try
            {
                if (!System.IO.File.Exists(_savedQueriesPath)) return new List<SavedQuery>();
                var json = System.IO.File.ReadAllText(_savedQueriesPath);
                return JsonSerializer.Deserialize<List<SavedQuery>>(json, _jsonOpts) ?? new List<SavedQuery>();
            }
            catch { return new List<SavedQuery>(); }
        }

        private static void PersistSavedQueries(List<SavedQuery> queries)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(_savedQueriesPath)!);
            System.IO.File.WriteAllText(_savedQueriesPath, JsonSerializer.Serialize(queries, _jsonOpts));
        }

        // GET api/database/saved-queries
        [HttpGet("saved-queries")]
        public IActionResult GetSavedQueries() => Ok(LoadSavedQueries());

        // POST api/database/saved-queries
        [HttpPost("saved-queries")]
        public IActionResult SaveQuery([FromBody] SaveQueryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Query name is required." });

            var queries = LoadSavedQueries();
            var entry   = new SavedQuery { Name = req.Name.Trim(), Sql = req.Sql };
            queries.Add(entry);
            PersistSavedQueries(queries);
            return Ok(entry);
        }

        // DELETE api/database/saved-queries/{id}
        [HttpDelete("saved-queries/{id}")]
        public IActionResult DeleteSavedQuery(string id)
        {
            var queries = LoadSavedQueries();
            var removed = queries.RemoveAll(q => q.Id == id);
            if (removed == 0) return NotFound(new { message = "Query not found." });
            PersistSavedQueries(queries);
            return Ok(new { message = "Query deleted." });
        }

        // POST api/database/query
        [HttpPost("query")]
        public async Task<IActionResult> RunQuery([FromBody] QueryRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Sql))
                return Ok(new { columns = Array.Empty<string>(), rows = Array.Empty<object>(), rowsAffected = 0, error = "SQL query cannot be empty." });

            try
            {
                var result = await DBConnect.ExecuteRawQuery(req.Sql);

                return Ok(new
                {
                    columns      = result.Columns,
                    rows         = result.Rows,
                    rowsAffected = result.RowsAffected,
                    error        = result.Error
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    columns      = Array.Empty<string>(),
                    rows         = Array.Empty<object>(),
                    rowsAffected = 0,
                    error        = ex.Message
                });
            }
        }

        // GET api/database/schema — returns cached schema from disk, or 404 if not built yet
        [HttpGet("schema")]
        public IActionResult GetSchema()
        {
            var schema = DBConnect.LoadSchemaCache();
            if (schema == null)
                return NotFound(new { message = "Schema has not been built yet. Use POST /api/database/schema/build." });

            return Ok(schema);
        }

        // POST api/database/schema/build — queries INFORMATION_SCHEMA and saves to disk
        [HttpPost("schema/build")]
        public async Task<IActionResult> BuildSchema()
        {
            var schema = await DBConnect.BuildAndSaveSchema();

            int totalTables = schema.Values.Sum(db => db.Count);
            int totalCols   = schema.Values.Sum(db => db.Values.Sum(t => t.Count));

            return Ok(new
            {
                message = $"Schema built: {schema.Count} databases, {totalTables} tables, {totalCols} columns.",
                databases = schema.Keys.ToArray()
            });
        }
    }
}

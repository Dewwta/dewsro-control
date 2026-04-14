using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class NoticeController : ControllerBase
    {
        public record AddNoticeRequest(string Subject, string Article, int ContentID = 22);

        // GET api/notice?contentId=22  (public – no auth required)
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetNotices([FromQuery] int contentId = 22)
        {
            var (success, notices, reason) = await DBConnect.GetNotices(contentId);
            if (!success)
                return StatusCode(500, new { message = reason });

            var result = notices!.Select(n => new
            {
                id        = n.ID,
                contentID = n.ContentID,
                subject   = n.Subject,
                article   = n.Article,
                editDate  = n.EditDate
            });

            return Ok(result);
        }

        // POST api/notice
        [HttpPost]
        public async Task<IActionResult> AddNotice([FromBody] AddNoticeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Subject))
                return BadRequest(new { message = "Subject is required." });
            if (string.IsNullOrWhiteSpace(req.Article))
                return BadRequest(new { message = "Article is required." });

            var (success, reason) = await DBConnect.AddNotice(req.Subject, req.Article, req.ContentID);
            if (!success)
                return BadRequest(new { message = reason });

            return Ok(new { message = "Notice added." });
        }

        // DELETE api/notice/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteNotice(int id)
        {
            var (success, reason) = await DBConnect.DeleteNotice(id);
            if (!success)
                return NotFound(new { message = reason });

            return Ok(new { message = "Notice deleted." });
        }
    }
}

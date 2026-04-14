using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/privileged-ip")]
    [RequireAdmin]
    public class PrivilegedIpController : ControllerBase
    {
        public record UpdateIpRequest(int NIdx, string Ip, bool IsGm);

        // GET api/privileged-ip
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var (success, items, reason) = await DBConnect.GetPrivelagedIps();
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(items!.Select(r => new
            {
                nIdx      = r.nIdx,
                szIPBegin = r.szIPBegin,
                szIPEnd   = r.szIPEnd,
                isGm      = r.szGM == "Yes",
                dIssueDate = r.dIssueDate,
                szISP     = r.szISP,
                szDesc    = r.szDesc
            }));
        }

        // PUT api/privileged-ip
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateIpRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Ip))
                return BadRequest(new { message = "IP address is required." });

            var (success, reason) = await DBConnect.ChangePrivelagedIp(body.NIdx, body.Ip, body.IsGm);
            if (!success) return StatusCode(500, new { message = reason });
            return Ok(new { message = reason });
        }
    }
}

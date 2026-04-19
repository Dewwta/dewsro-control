using CoreLib.Models;
using CoreLib.Models.Requests;
using CoreLib.Models.Responses;
using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.Utils;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(IConfiguration configuration) : ControllerBase
    {
        private readonly IConfiguration _configuration = configuration;

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest _req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_req.Username) || string.IsNullOrWhiteSpace(_req.Password))
                {
                    return BadRequest(new { message = "Username and password are required" });
                }

                var res = await DBConnect.AuthenticateUserAccount(_req.Username, _req.Password);

                if (res.obj == null)
                {
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                var token = JwtHelper.GenerateToken(res.obj.User!.Username, res.obj.User!.JID, _configuration);
                Logger.Info(this, $"User logged in: {res.obj.User!.Username}");

                return Ok(new LoginResponseObject
                {
                    Jwt = token,
                    User = res.obj.User!.ToSafeUser(),
                });
            }
            catch (Exception ex)
            {
                return Ok(new CommonResponse
                {
                    Success = false,
                    Message = $"Error occurred in endpoint: {ex.Message}"
                });
            }
        }

        [AllowAnonymous]
        [HttpPost("sign-up")]
        public async Task<IActionResult> SignUpNewUser([FromBody] SignupRequest _req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_req.Username))
                    return BadRequest(new { message = "Username is required" });
                if (string.IsNullOrWhiteSpace(_req.Password))
                    return BadRequest(new { message = "Password is required" });
                if (_req.Password.Length < 8)
                    return BadRequest(new { message = "Password must be at least 8 characters" });

                // invite code validate
                if (string.IsNullOrWhiteSpace(_req.InviteCode))
                    return BadRequest(new { message = "An invite code is required to create an account." });
                if (!VSRO.InviteCodeStore.TryConsume(_req.InviteCode))
                    return BadRequest(new { message = "Invalid or already-used invite code." });

                var res = await DBConnect.AddNewUser(_req.Username, _req.Password, false);
                if (!res.success)
                {
                    string msg = $"Add new user did not succeed: {res.reason}";
                    Logger.Error(this, msg);
                    return BadRequest(msg);
                }

                // fetch the newly created user to get the real JID before updating profile fields
                var userLookup = await DBConnect.GetUserAccountByUsername(_req.Username);
                if (userLookup.success && userLookup.user != null)
                {
                    userLookup.user.Email    = _req.Email;
                    userLookup.user.Nickname = _req.Nickname;
                    userLookup.user.Sex      = _req.Sex;
                    await DBConnect.UpdateUserAccount(userLookup.user);
                }

                return Ok(new CommonResponse
                {
                    Success = true,
                    Message = $"Signup Successful"
                });

            }
            catch (Exception ex)
            {
                return Ok(new CommonResponse
                {
                    Success = false,
                    Message = $"Error occurred in endpoint: {ex.Message}"
                });
            }
        }

        [RequireAuth]
        [HttpGet("me")]
        public IActionResult GetMe()
        {
            var user = HttpContext.Items["User"] as UserDTO;
            if (user == null) return Unauthorized(new { message = "Authentication required." });
            return Ok(user.ToSafeUser());
        }

        public record UpdateProfileRequest(string? Nickname, string? Email, string? Sex);

        [RequireAuth]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest _req)
        {
            try
            {
                var user = HttpContext.Items["User"] as UserDTO;
                if (user == null) return Unauthorized(new { message = "Authentication required." });

                user.Nickname = _req.Nickname;
                user.Email    = _req.Email;
                user.Sex      = _req.Sex;

                var res = await DBConnect.UpdateUserAccount(user);
                if (!res.success)
                    return BadRequest(new { message = res.reason });

                return Ok(new { message = "Profile updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

        [RequireAuth]
        [HttpGet("silk")]
        public async Task<IActionResult> GetSilk()
        {
            var user = HttpContext.Items["User"] as UserDTO;
            if (user == null) return Unauthorized(new { message = "Authentication required." });

            var res = await DBConnect.GetUserSilkByJID(user.JID);
            if (!res.success)
                return Ok(new { silk = 0 });

            return Ok(new { silk = res.silk });
        }


        [RequireAdmin]
        [HttpGet("invite-codes")]
        public IActionResult ListInviteCodes()
        {
            var codes = VSRO.InviteCodeStore.GetAll()
                .Select(c => new { code = c.Code, createdAt = c.CreatedAt, note = c.Note });
            return Ok(codes);
        }

        public record GenerateInviteCodeRequest(string? Note);

        [RequireAdmin]
        [HttpPost("invite-codes")]
        public IActionResult GenerateInviteCode([FromBody] GenerateInviteCodeRequest? req)
        {
            string code = VSRO.InviteCodeStore.Generate(req?.Note ?? "");
            return Ok(new { code, message = "Invite code generated." });
        }

        [RequireAdmin]
        [HttpDelete("invite-codes/{code}")]
        public IActionResult RevokeInviteCode(string code)
        {
            bool removed = VSRO.InviteCodeStore.Revoke(code);
            if (!removed)
                return NotFound(new { message = "Code not found." });
            return Ok(new { message = $"Invite code '{code}' revoked." });
        }

        [RequireAuth]
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest _req)
        {
            try
            {
                var username = HttpContext.Items["Username"] as string;
                if (string.IsNullOrEmpty(username)) return Unauthorized(new { message = "Authentication required." });

                if (_req.NewPassword.Length < 8)
                    return BadRequest(new { message = "New password must be at least 8 characters." });

                var res = await DBConnect.ChangeUserPassword(username, _req.CurrentPassword, _req.NewPassword);
                if (!res.success)
                    return BadRequest(new { message = res.reason });

                return Ok(new { message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

    }
}

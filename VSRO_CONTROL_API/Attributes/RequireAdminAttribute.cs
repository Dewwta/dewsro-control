using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VSRO_CONTROL_API.Utils;
using VSRO_CONTROL_API.VSRO;

namespace VSRO_CONTROL_API.Attributes
{
    public class RequireAdminAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor.EndpointMetadata
                .Any(m => m is AllowAnonymousAttribute))
            {
                await next();
                return;
            }

            var logger = context.HttpContext.RequestServices.GetService<ILogger<RequireAuthAttribute>>();
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var username = JwtHelper.ValidateToken(token, context);

                if (!string.IsNullOrEmpty(username))
                {
                    var res = await DBConnect.GetUserAccountByUsername(username);

                    if (res.user != null && res.user.IsAuthoritive())
                    {
                        context.HttpContext.Items["User"] = res.user;
                        context.HttpContext.Items["Username"] = res.user.Username;

                        await next();
                        return;
                    }
                }
            }

            context.Result = new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

    }
}

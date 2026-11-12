using QuickEditer.Core;
using QuickEditer.Services;

namespace QuickEditer.Services
{
    public class ApiAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            var authSvc = context.RequestServices.GetRequiredService<AuthService>();
            var token = context.Request.Headers["X-Auth-Token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
                token = context.Request.Cookies["qe_auth"];

            if (!authSvc.ValidateAuthToken(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await _next(context);
        }
    }
}
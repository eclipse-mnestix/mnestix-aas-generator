using System.Security.Claims;

namespace MnestixApi.Middlewares;

/// <summary>
/// This middleware class is responsible for handling the default authentication
/// </summary>
public static class DefaultAuthenticationMiddleware
{
    internal static Func<HttpContext, Func<Task>, Task> ConfigureDefaultAuthenticationHandling()
    {
        return async (context, next) =>
        {
            var scopeClaim = new Claim("scp", "admin.write");
            var defaultUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new(ClaimTypes.Name, "DefaultUser"),
                scopeClaim
            }, "DefaultAuthentication"));
            context.User = defaultUser;

            await next();
        };
    }
}
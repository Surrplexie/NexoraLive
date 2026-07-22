using Microsoft.AspNetCore.Http;
using NL.Server;

namespace NL.Web.Shared;

/// <summary>Phase K — rate limits public admit and read-heavy endpoints by client IP.</summary>
public static class NlPublicRateLimitMiddleware
{
    public static WebApplication UseNlPublicRateLimits(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var limits = context.RequestServices.GetService<NlPublicRateLimitService>();
            if (limits is null || !limits.IsEnabled)
            {
                await next();
                return;
            }

            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method.ToUpperInvariant();
            var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (method == "POST" && path.Equals("/api/v1/session/admit", StringComparison.OrdinalIgnoreCase))
            {
                if (!limits.TryAdmit(clientKey))
                {
                    await WriteRateLimitedAsync(context);
                    return;
                }
            }
            else if (method == "GET" && IsPublicReadPath(path))
            {
                if (!limits.TryPublicRead(clientKey))
                {
                    await WriteRateLimitedAsync(context);
                    return;
                }
            }

            await next();
        });

        return app;
    }

    private static bool IsPublicReadPath(string path)
    {
        if (path.StartsWith("/api/v1/spectator/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Equals("/api/v1/moderation/recent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/api/v1/moderation/players/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/history", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Equals("/api/v1/demo/status", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/ops/status", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteRateLimitedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = "60";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded. Try again later.",
        });
    }
}

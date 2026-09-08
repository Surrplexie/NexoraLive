using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NL.Core.Security;

namespace NL.Web.Shared;

public static class NlWebSecurityExtensions
{
    public static IServiceCollection AddNlWebSecurity(this IServiceCollection services, NlSecuritySettings settings)
    {
        services.AddSingleton(settings);
        services.AddCors(options => options.AddDefaultPolicy(policy => ConfigureCors(policy, settings)));
        return services;
    }

    public static WebApplication UseNlOperatorAuth(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var settings = context.RequestServices.GetRequiredService<NlSecuritySettings>();
            if (NlSecurityPaths.RequiresOperatorAuth(context.Request.Method, context.Request.Path.Value)
                && !IsAuthorized(context, settings))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Operator authentication required. Provide X-NL-Operator-Key or Authorization: Bearer.",
                });
                return;
            }

            await next();
        });

        return app;
    }

    public static bool IsAuthorized(HttpContext context, NlSecuritySettings? settings = null)
    {
        settings ??= context.RequestServices.GetRequiredService<NlSecuritySettings>();
        return NlOperatorAuth.IsAuthorized(
            settings,
            context.Request.Headers[NlOperatorAuth.HeaderName],
            context.Request.Headers.Authorization.ToString());
    }

    private static void ConfigureCors(CorsPolicyBuilder policy, NlSecuritySettings settings)
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (settings.CorsOrigins.Count == 0 && !settings.PublicMode)
        {
            policy.SetIsOriginAllowed(_ => true);
            return;
        }

        if (settings.CorsOrigins.Count == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
            return;
        }

        policy.WithOrigins(settings.CorsOrigins.ToArray());
    }
}

using Microsoft.AspNetCore.StaticFiles;

namespace Everdue.Server.Api;

/// <summary>
/// Response hardening for a single-origin SPA. None of this replaces the auth checks — it removes
/// the classes of attack that do not need a bug in the app: MIME sniffing, framing, referrer leaks
/// and injected script.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// A same-origin SPA needs nothing external at runtime, so everything is <c>'self'</c>.
    /// Style needs <c>'unsafe-inline'</c> because Mantine sets CSS custom properties inline;
    /// script deliberately does not, which is the half that matters for XSS.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseEverdueSecurityHeaders(this WebApplication app, bool requireHttps)
    {
        // The API reference UI in Development pulls its bundle from a CDN, so the strict policy is
        // applied everywhere else. Every other header applies in all environments.
        var applyCsp = !app.Environment.IsDevelopment();

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), interest-cohort=()";

            if (applyCsp)
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            // Only meaningful once TLS actually terminates here; announcing it from an HTTP-only
            // install would lock users out of their own instance.
            if (requireHttps)
            {
                headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            }

            await next();
        });
    }

    /// <summary>
    /// Vite emits content-hashed asset filenames, so those can be cached forever — a new build has
    /// new names. The shell that points at them must never be cached, or a browser keeps loading
    /// last week's app.
    /// </summary>
    public static void ApplyStaticFileCaching(StaticFileResponseContext context)
    {
        var isHashedAsset = context.Context.Request.Path.StartsWithSegments("/assets");

        context.Context.Response.Headers.CacheControl = isHashedAsset
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }
}

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Everdue.Server.Application.Attachments;
using Everdue.Server.Application.Imports;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.ApiKeys;
using Everdue.Server.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Everdue.Server.Api;

public static class ApiPolicies
{
    public const string Admin = "everdue:admin";

    /// <summary>Rate-limit partition for the endpoints an unauthenticated caller can reach.</summary>
    public const string AuthRateLimit = "everdue:auth";

    /// <summary>
    /// Per-key window for programmatic callers. Returns no limiter for a cookie session, so nothing about a
    /// browser request changes.
    /// </summary>
    public const string ApiKeyRateLimit = "everdue:apikey";
}

public static class ApiServiceCollectionExtensions
{
    /// <summary>Auth, authorization, JSON, ProblemDetails and OpenAPI. Program.cs calls this and nothing else.</summary>
    public static IServiceCollection AddEverdueApi(this IServiceCollection services, IConfiguration configuration)
    {
        var requireHttps = configuration.GetValue($"{SecurityOptions.Section}:RequireHttps", false);

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options => ConfigureCookie(options, requireHttps));
        services.AddEverdueGoogleAuth(configuration, requireHttps);

        // The public API's second scheme. Registering it here rather than editing every endpoint module is the
        // point of widening the two policies below: no `RequireAuthorization()` call site changes.
        services.AddAuthentication()
            .AddScheme<ApiKeySchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        // Both schemes on both policies. Which endpoints a key may actually reach is decided by endpoint
        // metadata and `ApiKeyGate`, not by the policy — a key whose actor is an administrator still cannot
        // reach /users.
        string[] schemes = [IdentityConstants.ApplicationScheme, ApiKeyAuthenticationHandler.SchemeName];

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(null)
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(schemes).RequireAuthenticatedUser().Build())
            .AddPolicy(ApiPolicies.Admin, policy => policy
                .AddAuthenticationSchemes(schemes)
                .RequireRole(nameof(UserRole.Admin)));

        services.Configure<JsonOptions>(options =>
        {
            // Enums travel as names. The generated TypeScript types then read like the domain does,
            // and a reordered enum can never silently change meaning on the wire.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddEverdueRateLimiting(
            configuration.GetValue($"{SecurityOptions.Section}:LoginAttemptsPerMinute", 30),
            configuration.GetValue($"{SecurityOptions.Section}:ApiRequestsPerMinute", 600));

        services.AddEverdueCompression();
        services.AddEverdueUploadLimits(configuration);

        services.AddProblemDetails();
        services.AddExceptionHandler<AppExceptionHandler>();
        services.AddOpenApi("v1", options => options.AddSchemaTransformer(NormalizeSchema));

        return services;
    }

    /// <summary>
    /// .NET's JSON schema exporter describes an int32 as <c>["integer","string"]</c> (because
    /// System.Text.Json would accept a quoted number) and folds nullability into the enum list.
    /// Both are true and both are useless downstream: the client's generated types come out as
    /// <c>number | string</c> and <c>… | null</c>, which then have to be defended against in every
    /// component. Normalising here keeps the contract honest and the generated TypeScript clean.
    /// </summary>
    private static Task NormalizeSchema(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Type is { } type
            && (type & JsonSchemaType.String) != 0
            && (type & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0)
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        if (schema.Enum is { Count: > 0 } values)
        {
            // Nullability stays where it belongs: on the property that is optional, not on the enum.
            for (var index = values.Count - 1; index >= 0; index--)
            {
                if (values[index] is null || values[index]!.GetValueKind() == JsonValueKind.Null)
                {
                    values.RemoveAt(index);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Identity's lockout stops someone hammering <em>one</em> account. It does nothing against the
    /// opposite shape — one common password tried against every account — so sign-in also gets a
    /// window limit. Deliberately generous: a whole office signing in at 09:00 must never hit it,
    /// while an automated spray (thousands of attempts) does immediately.
    ///
    /// Behind a reverse proxy every request appears to come from the proxy, so the partition
    /// degrades to one shared bucket. That is still the right trade at this limit.
    /// </summary>
    private static IServiceCollection AddEverdueRateLimiting(
        this IServiceCollection services,
        int attemptsPerMinute,
        int apiRequestsPerMinute)
        => services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ApiPolicies.AuthRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = attemptsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Partitioned by key, and *only* for key callers: a cookie session on the same endpoint gets no
            // limiter, so a person clicking around is never rationed because a script was busy.
            options.AddPolicy(ApiPolicies.ApiKeyRateLimit, context =>
            {
                var keyId = context.User.FindFirst(ApiKeyAuthenticationHandler.KeyIdClaim)?.Value;

                return keyId is null
                    ? RateLimitPartition.GetNoLimiter("cookie")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        keyId,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = apiRequestsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        });
            });
        });

    /// <summary>
    /// The SPA bundle is ~900 KB uncompressed and was being served raw. Compression is enabled for
    /// HTTPS too: BREACH needs a secret inside a compressed response to extract, and this API puts
    /// none there — the auth cookie is HttpOnly and there is no CSRF token to steal.
    /// </summary>
    private static IServiceCollection AddEverdueCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            [
                "application/json",
                "application/problem+json",
                "image/svg+xml",
            ]);
        });

        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

        return services;
    }

    /// <summary>
    /// Google sign-in, registered only when it is configured — an unconfigured handler would answer
    /// the challenge route with a 500 instead of the honest 404 the endpoint returns.
    ///
    /// Two details make this work rather than "correlation failed": the handler signs into Identity's
    /// **external** scheme (so the callback can read the principal with the ordinary SignInManager),
    /// and the correlation cookie is **Lax** rather than the framework's None. None additionally
    /// requires Secure, which silently drops the cookie behind a proxy that terminates TLS upstream;
    /// Lax survives the top-level GET redirect back from Google, which is all this needs.
    ///
    /// The app cookie stays SameSite=Strict. See ExternalAuthEndpoints for how the callback lands
    /// without needing it.
    /// </summary>
    private static IServiceCollection AddEverdueGoogleAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        bool requireHttps)
    {
        var clientId = configuration[$"{GoogleAuthOptions.Section}:ClientId"];
        var clientSecret = configuration[$"{GoogleAuthOptions.Section}:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return services;
        }

        if (!requireHttps)
        {
            // Google refuses non-localhost http redirect URIs, so this combination works on a
            // developer's machine and nowhere else. Saying so at startup beats a "redirect_uri
            // mismatch" that looks like a Google problem.
            services.AddSingleton<IStartupWarning>(new StartupWarning(
                "Auth:Google is configured but Security:RequireHttps is false. Google only accepts https " +
                "redirect URIs outside localhost, so sign-in will fail on any real deployment."));
        }

        services.AddAuthentication().AddGoogle(options =>
        {
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.SignInScheme = IdentityConstants.ExternalScheme;

            // Google refuses non-localhost http redirect URIs, so this path is https in every real
            // deployment: https://{host}/signin-google, registered per installation.
            options.CallbackPath = "/signin-google";

            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = requireHttps
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;

            // Everything the sign-in decision depends on: the address, and whether Google vouches for it.
            options.Scope.Add("email");
            options.ClaimActions.MapJsonKey("email_verified", "email_verified", "boolean");
        });

        return services;
    }

    /// <summary>
    /// Makes the configured attachment limit the *actual* limit.
    ///
    /// Checking a file's length inside the handler only rejects what the transport already agreed to
    /// receive: without this, Kestrel's own 30 MB default decides, and an install that set 2 MB would
    /// still stream 30 MB off the wire before anything said no. Attachments are the only large body
    /// this API accepts, so bounding it here bounds the right thing.
    /// </summary>
    private static IServiceCollection AddEverdueUploadLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var attachmentMax = configuration.GetValue($"{AttachmentOptions.Section}:MaxSizeBytes", 10L * 1024 * 1024);
        var importMax = configuration.GetValue($"{ImportOptions.Section}:MaxSizeBytes", 2L * 1024 * 1024);

        // The larger of the two: whichever limit were lower would otherwise silently decide the other feature's
        // ceiling, and "attachments are 10 MB but uploads stop at 2 MB" is a bug nobody would look for here.
        var maxSize = Math.Max(attachmentMax, importMax);

        // Slack for the multipart envelope: boundaries, headers and the filename itself.
        var ceiling = maxSize + (1024 * 1024);

        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = ceiling);

        services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = ceiling);

        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options, bool requireHttps)
    {
        options.Cookie.Name = "everdue.auth";
        options.Cookie.HttpOnly = true;

        // This is the entire CSRF posture: a same-origin SPA, a strict-same-site cookie and no CORS.
        // No tokens, no double-submit, nothing to get wrong.
        options.Cookie.SameSite = SameSiteMode.Strict;

        // `Always` marks the cookie Secure even on an HTTP response, which is right when TLS
        // terminates at the app and fatal when it does not — the browser would simply drop the
        // cookie and login would fail with no error anywhere. Opt in with Security:RequireHttps.
        options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        // An API answers with status codes, never with a redirect to a login page that does not exist.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}

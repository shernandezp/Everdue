using Everdue.Server.Api.Endpoints;

namespace Everdue.Server.Api;

public static class EverdueEndpointRouteBuilderExtensions
{
    /// <summary>
    /// One line per resource. Program.cs stays a wiring file; every route lives next to the resource
    /// it belongs to, in Api/Endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapEverdueApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapAuthEndpoints();
        api.MapExternalAuthEndpoints();
        api.MapEntityEndpoints();
        api.MapDepartmentEndpoints();
        api.MapResponsibilityEndpoints();
        api.MapWorkItemEndpoints();
        api.MapCommentEndpoints();
        api.MapAttachmentEndpoints();
        api.MapReportEndpoints();
        api.MapInsightsEndpoints();
        api.MapUserEndpoints();
        api.MapSettingsEndpoints();
        api.MapChannelEndpoints();
        api.MapNotificationEndpoints();
        api.MapMeEndpoints();
        api.MapDigestSubscriptionEndpoints();
        api.MapSavedViewEndpoints();

        // v2.5.
        api.MapChecklistEndpoints();
        api.MapEntityFieldEndpoints();
        api.MapExportEndpoints();
        api.MapImportEndpoints();
        api.MapApiKeyEndpoints();
        api.MapWebhookEndpoints();
        api.MapMetaEndpoints();
        api.MapDemoEndpoints();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health").AllowAnonymous();

        return app;
    }
}

using System.Reflection;
using Everdue.Server.Domain;

namespace Everdue.Server.Api.Endpoints;

/// <summary>The language codes this build can render, and their names in themselves.</summary>
public sealed record LanguageDto(string Code, string NativeName);

/// <summary>
/// The AGPL's "Appropriate Legal Notices" (§5d), as data the SPA renders in its footer: what this is, what
/// licence it is under, that it comes with no warranty, and where the source is.
/// </summary>
public sealed record AboutDto(
    string Product,
    string Version,
    string License,
    string LicenseUrl,
    string SourceUrl,
    string Warranty);

public static class MetaEndpoints
{
    /// <summary>
    /// Anonymous on purpose: the login screen has a language picker and a licence notice, and neither is
    /// privileged information.
    /// </summary>
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/languages", () => Results.Ok(
                Languages.Supported.Select(code => new LanguageDto(code, Languages.NativeName(code)))))
            .WithTags("Meta")
            .AllowAnonymous()
            .WithSummary("The server owns the supported-language list; the client's picker renders from it, so the two cannot disagree.")
            .Produces<IReadOnlyList<LanguageDto>>();

        api.MapGet("/about", () => Results.Ok(new AboutDto(
                "Everdue",
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown",
                "GNU Affero General Public License v3.0 or later",
                "https://www.gnu.org/licenses/agpl-3.0.html",
                SourceUrl,
                "This program comes with ABSOLUTELY NO WARRANTY, to the extent permitted by applicable law.")))
            .WithTags("Meta")
            .AllowAnonymous()
            .WithSummary("Licence, version and source location. The AGPL requires an interactive interface to say so.")
            .Produces<AboutDto>();

        return api;
    }

    /// <summary>
    /// Where the corresponding source is. AGPL §13 requires that a user interacting with a modified version over
    /// a network can get its source, so a fork that changes this file should change this URL too.
    /// </summary>
    public const string SourceUrl = "https://github.com/shernandezp/everdue";
}

namespace Everdue.Server.Domain;

/// <summary>
/// A named filter set. Stores the raw query string rather than parsed parameters: the list screen is
/// already URL-driven, so applying a view is handing the string back to the router — no serialization
/// format to invent, and a filter added later works in old saved views for free.
///
/// Personal only. Shared or team views are a permissions question, and permissions are out of scope for now.
/// </summary>
public class SavedView : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Which screen the view belongs to: <c>work</c> or <c>board</c>.</summary>
    public string Route { get; set; } = SavedViewRoutes.Work;

    /// <summary>The query string without its leading '?'.</summary>
    public string QueryString { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public static class SavedViewRoutes
{
    public const string Work = "work";
    public const string Board = "board";

    public static readonly string[] Supported = [Work, Board];

    public static bool IsSupported(string? route)
        => route is not null && Supported.Contains(route, StringComparer.OrdinalIgnoreCase);
}

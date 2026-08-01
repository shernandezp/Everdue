namespace Everdue.Server.Application.Common;

/// <summary>
/// The field names written into a work-item event's diff payload.
///
/// These are a stored contract, not labels. They go into <c>WorkItemEvents.DataJson</c> and are read
/// back by the drawer's history, which maps each one to a translation key — so renaming a value here
/// stops years of existing history from rendering, and the compiler cannot tell you. Naming them once
/// is what keeps the writer, the reassignment rule that looks for the owner field, and the client's
/// label map from drifting apart.
/// </summary>
public static class WorkItemFields
{
    public const string Title = "title";
    public const string Description = "description";
    public const string Owner = "ownerUserId";
    public const string Entity = "entityId";
    public const string Department = "departmentId";
}

/// <summary>
/// How a work item came to exist, recorded on its <see cref="Domain.WorkItemEventType.Created"/> event.
/// Stored, like <see cref="WorkItemFields"/>, so the values outlive the code that wrote them.
/// </summary>
public static class WorkItemSources
{
    /// <summary>Somebody typed it in.</summary>
    public const string OneOff = "one-off";

    /// <summary>The occurrence engine spawned it from a responsibility.</summary>
    public const string Engine = "engine";
}

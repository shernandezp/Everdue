using System.Globalization;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;

namespace Everdue.Server.Application.Reports;

/// <summary>
/// Turns the very <see cref="ListWorkItemsQuery"/> a report used to count rows into the query string
/// the UI navigates to. The number and the list are produced by the same object, so the guarantee
/// that every dashboard number drills through to a list totalling exactly that number holds by
/// construction rather than by discipline.
/// </summary>
public static class DrillThroughFactory
{
    public static DrillThrough For(ListWorkItemsQuery query)
    {
        var parameters = new Dictionary<string, string>();

        if (query.ResolvedView != WorkItemView.List)
        {
            parameters["view"] = "board";
        }

        Add(parameters, "ownerId", query.OwnerId);
        Add(parameters, "departmentId", query.DepartmentId);
        Add(parameters, "entityId", query.EntityId);
        Add(parameters, "responsibilityId", query.ResponsibilityId);

        if (query.Occurrences is { } occurrences)
        {
            parameters["occurrences"] = occurrences ? "true" : "false";
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            parameters["entityType"] = query.EntityType;
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            parameters["status"] = query.Status;
        }

        if (!string.IsNullOrWhiteSpace(query.HoldReason))
        {
            parameters["holdReason"] = query.HoldReason;
        }

        Add(parameters, "dueFrom", query.DueFrom);
        Add(parameters, "dueTo", query.DueTo);
        Add(parameters, "completedFrom", query.CompletedFrom);
        Add(parameters, "completedTo", query.CompletedTo);

        if (query.Overdue == true)
        {
            parameters["overdue"] = "true";
        }

        if (query.IncludeCancelled)
        {
            parameters["includeCancelled"] = "true";
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters["search"] = query.Search;
        }

        return new DrillThrough(parameters);
    }

    private static void Add(IDictionary<string, string> parameters, string key, Guid? value)
    {
        if (value is { } id)
        {
            parameters[key] = id.ToString();
        }
    }

    private static void Add(IDictionary<string, string> parameters, string key, DateTimeOffset? value)
    {
        if (value is { } instant)
        {
            parameters[key] = instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
    }
}

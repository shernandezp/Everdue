using System.Globalization;

namespace Everdue.Server.Application.Common;

/// <summary>
/// SPA paths the *server* has to know, because it puts them in things the user clicks: a deep link in
/// a notification e-mail, the page an external sign-in returns to.
///
/// These mirror routes declared in the client's router. Keeping the handful the server needs in one
/// place means a renamed screen is one grep away from the links that would otherwise 404 silently —
/// nobody notices a dead link in an e-mail they did not send.
/// </summary>
public static class ClientRoutes
{
    /// <summary>Where an external sign-in hands control back to the SPA.</summary>
    public const string ExternalLoginComplete = "/login/complete";

    private const string WorkList = "/work";

    private const string WorkItemQueryParameter = "workItemId";

    /// <summary>The work list, opened on one item — the drawer the notification is about.</summary>
    public static string WorkItem(Guid workItemId)
        => $"{WorkList}?{WorkItemQueryParameter}={workItemId.ToString(null, CultureInfo.InvariantCulture)}";
}

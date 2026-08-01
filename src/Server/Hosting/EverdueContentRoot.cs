namespace Everdue.Server.Hosting;

/// <summary>
/// Where the app looks for <c>appsettings.json</c> and <c>wwwroot</c>.
///
/// ASP.NET Core defaults the content root to the <em>current working directory</em>, which is fine
/// when someone runs the binary from its own folder and wrong everywhere else: <c>sc.exe</c> starts
/// a Windows service in <c>System32</c>, and a systemd unit uses whatever <c>WorkingDirectory</c>
/// it was given. In both cases the API would come up while the SPA silently 404s — the failure is
/// invisible until a user opens the site.
///
/// Anchoring to the binary makes "copy one file plus appsettings.json and run it" true from any
/// directory. For a single-file publish <see cref="AppContext.BaseDirectory"/> is the executable's
/// own folder, not the extraction directory, so this is correct there too.
/// </summary>
public static class EverdueContentRoot
{
    /// <summary>Returns null when the host was told explicitly where the content root is, so that wins.</summary>
    public static string? Resolve(string[] args)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_CONTENTROOT")))
        {
            return null;
        }

        foreach (var argument in args)
        {
            if (argument.Contains("contentroot", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return AppContext.BaseDirectory;
    }
}

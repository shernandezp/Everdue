namespace Everdue.Server.Infrastructure.Demo;

/// <summary>
/// Demo mode. Off unless somebody explicitly asks for it, and a no-op on a database that already holds work —
/// a seeder that can overwrite a real install is a data-loss bug waiting for a typo in a compose file.
/// </summary>
public sealed class DemoOptions
{
    public const string Section = "Demo";

    /// <summary>
    /// Seeds a believable six months of operation on first start. Explicit opt-in: there is no heuristic that
    /// makes this safe to guess.
    /// </summary>
    public bool Seed { get; set; }

    /// <summary>
    /// The password every seeded demo account shares. Well-known by design — it is a demo — which is exactly
    /// why the flag has to be set on purpose and why the seeder logs loudly what it created.
    /// </summary>
    public string Password { get; set; } = "EverdueDemo2026!";

    /// <summary>How much history to write. Six months is enough for compliance trends and hold aging to read.</summary>
    public int Months { get; set; } = 6;

    /// <summary>
    /// May an administrator turn demo mode on or off from inside the running app? On by default, because the
    /// feature is worthless to the person evaluating Everdue if it needs a restart and a config file.
    ///
    /// <para>Set it to false on a production install. The toggle then does not exist at all — the endpoint
    /// answers 404 and the card is never rendered — which is a stronger guarantee than a confirmation dialog:
    /// an irreversible button that is absent cannot be clicked by a tired administrator at 18:00 on a Friday.</para>
    /// </summary>
    public bool AllowReset { get; set; } = true;
}

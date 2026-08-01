namespace Everdue.Server.Application.Abstractions;

/// <summary>What a wipe removed. Reported back so the confirmation the administrator gave has a receipt.</summary>
public sealed record DemoResetSummary(
    int WorkItems,
    int Responsibilities,
    int Entities,
    int Departments,
    int Users,
    int Attachments,
    int Notifications);

/// <summary>What a seed wrote.</summary>
public sealed record DemoSeedSummary(
    int Users,
    int Entities,
    int Responsibilities,
    int Occurrences,
    int Tasks,
    string Password);

/// <summary>
/// Demo mode's two irreversible operations. Both are Infrastructure work — one walks every table in the
/// model plus the file store, the other creates Identity users — so the Application layer states what it
/// needs and nothing about how it happens.
///
/// <para>There is deliberately no "undo". A wipe is a wipe; the only honest recovery is the backup the
/// administrator was told to take, which is why the confirmation the handler demands is as heavy as it is.</para>
/// </summary>
public interface IDemoMode
{
    /// <summary>
    /// False when <c>Demo:AllowReset</c> is off. A production self-hoster can remove the capability from the
    /// install entirely rather than trusting that no administrator ever clicks it — an irreversible button
    /// that cannot exist is safer than one guarded by a dialog.
    /// </summary>
    bool ResetAllowed { get; }

    /// <summary>The password every seeded demo account will share, so the UI can show it before wiping anything.</summary>
    string DemoPassword { get; }

    /// <summary>
    /// Deletes every trace of this tenant's work — the whole ledger included — keeping only
    /// <paramref name="keepUserId"/>, who is the administrator asking. Attachment bytes go too: leaving orphaned
    /// files behind would mean a "wiped" install still holds the photographs.
    /// </summary>
    Task<DemoResetSummary> WipeAsync(Guid keepUserId, CancellationToken cancellationToken = default);

    /// <summary>Writes the demo dataset. Call after <see cref="WipeAsync"/> — it assumes an empty tenant.</summary>
    Task<DemoSeedSummary> SeedAsync(CancellationToken cancellationToken = default);
}

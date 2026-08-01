namespace Everdue.Server.Application.Contracts;

/// <summary>
/// What the settings screen needs in order to draw the demo card honestly: whether this install holds demo
/// data, whether it is allowed to change that, and the exact string the administrator will have to type.
/// </summary>
/// <param name="Enabled">This tenant currently holds seeded demo data.</param>
/// <param name="ResetAllowed">
/// False when <c>Demo:AllowReset</c> is off. The client hides the card entirely; the command endpoint answers
/// 404 regardless of what the client does with this.
/// </param>
/// <param name="ConfirmationPhrase">
/// The tenant's own name — what <c>DemoModeCommand.Confirmation</c> must match. Sent so the dialog can show
/// it, never so the client can decide anything: the comparison happens on the server.
/// </param>
/// <param name="DemoPassword">The password the seeded accounts will share, so the dialog can show it up front.</param>
public sealed record DemoStatusDto(
    bool Enabled,
    bool ResetAllowed,
    string ConfirmationPhrase,
    string DemoPassword);

/// <summary>
/// The receipt for an irreversible action: what was destroyed, and what replaced it. The counts are the only
/// evidence the administrator will ever get that the thing they confirmed is the thing that happened.
/// </summary>
public sealed record DemoModeResultDto(
    DemoStatusDto Status,
    DemoWipeCountsDto Deleted,
    DemoSeedCountsDto? Seeded);

public sealed record DemoWipeCountsDto(
    int WorkItems,
    int Responsibilities,
    int Entities,
    int Departments,
    int Users,
    int Attachments,
    int Notifications);

/// <summary>Null when demo mode was switched off — nothing is seeded on the way out.</summary>
public sealed record DemoSeedCountsDto(
    int Users,
    int Entities,
    int Responsibilities,
    int Occurrences,
    int Tasks,
    string Password);

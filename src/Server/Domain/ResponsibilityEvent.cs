namespace Everdue.Server.Domain;

/// <summary>
/// Append-only history for the rules that generate the ledger. A responsibility edit can change what
/// the ledger will record forever after (a rule change, a moved start date, a deactivation), so it
/// gets the same who/field/old-value trail a work-item edit has always had.
/// </summary>
public class ResponsibilityEvent : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid ResponsibilityId { get; set; }

    /// <summary>Never null: only users edit responsibilities — the engine reads them.</summary>
    public Guid UserId { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public ResponsibilityEventType EventType { get; set; }

    /// <summary>Free-form JSON payload (field diffs, pause window, …).</summary>
    public string? DataJson { get; set; }

    public Responsibility? Responsibility { get; set; }
}

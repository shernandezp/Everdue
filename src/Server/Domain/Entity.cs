namespace Everdue.Server.Domain;

/// <summary>
/// The thin reference table: name, type, active. Nothing more — the moment this grows credit
/// limits, contact persons or serial numbers, the ERP drift has begun.
/// </summary>
public class Entity : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public EntityType Type { get; set; }
    public bool Active { get; set; } = true;

    /// <summary>
    /// Display-only reference values, keyed by <see cref="EntityFieldDef"/> id — the account manager on
    /// a customer, the serial number on a machine. One JSON column and no EAV table, because nothing
    /// ever queries, sorts or reports on them: see <see cref="EntityCustomFields"/> and the guardrail
    /// note on <see cref="EntityFieldDef"/>. This column is the single, bounded exception to "nothing
    /// more" above, and it stays display-only.
    /// </summary>
    public string? CustomFieldsJson { get; set; }
}

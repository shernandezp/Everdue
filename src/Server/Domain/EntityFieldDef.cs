namespace Everdue.Server.Domain;

/// <summary>
/// One custom field an entity of a given type may carry — the account manager on a customer, the
/// serial number on a machine.
///
/// This is the closest the product comes to the ERP drift the guardrails exist to prevent, so it is
/// bounded on purpose: four scalar types, a hard cap per entity type, values in a single JSON column,
/// and <strong>no</strong> filter, sort, report column, insight metric or webhook field anywhere.
/// They are display-only reference information. The moment a custom field <em>does</em> something,
/// entities have stopped being thin.
/// </summary>
public class EntityFieldDef : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    /// <summary>Which kind of entity carries this field. A customer's fields are not a machine's.</summary>
    public EntityType EntityType { get; set; }

    public string Name { get; set; } = string.Empty;

    public EntityFieldType FieldType { get; set; }

    /// <summary>
    /// A JSON array of strings, for <see cref="EntityFieldType.Select"/> only. Parsed in memory —
    /// no JSON predicate is ever pushed to the database on either provider.
    /// </summary>
    public string? OptionsJson { get; set; }

    public int Position { get; set; }

    public bool Active { get; set; } = true;
}

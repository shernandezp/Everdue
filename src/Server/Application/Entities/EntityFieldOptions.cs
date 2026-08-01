using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Entities;

/// <summary>
/// The cap that keeps custom fields from becoming a customer record. Enforced by the handler, which is
/// why it lives in Application — the same split the other option classes use.
/// </summary>
public sealed class EntityFieldOptions
{
    public const string Section = "EntityFields";

    /// <summary>
    /// Ten per entity type. Not a performance limit — a limit on ambition: an entity that carries thirty
    /// fields has stopped being a reference and become the business data the guardrails exclude.
    /// </summary>
    [Range(1, 25)]
    public int MaxPerEntityType { get; set; } = 10;
}

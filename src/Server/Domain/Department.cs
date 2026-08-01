namespace Everdue.Server.Domain;

/// <summary>
/// Which team executes the work. Deliberately distinct from an <see cref="Entity"/> of type
/// <see cref="EntityType.Department"/>, which is what the work is <em>about</em>.
/// </summary>
public class Department : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}

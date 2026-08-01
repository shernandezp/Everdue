namespace Everdue.Server.Domain;

/// <summary>Create, list, delete (own or Admin). No editing in v1 — comments are a record, not a document.</summary>
public class Comment : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public WorkItem? WorkItem { get; set; }
}

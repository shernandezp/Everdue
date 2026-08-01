using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? DisplayName { get; }

    UserRole? Role { get; }

    bool IsAuthenticated { get; }

    bool IsAdmin { get; }

    /// <summary>
    /// Set when the caller presented an API key rather than a cookie. Recorded in every event the request
    /// writes, so "a script did this, acting as Ana" is answerable — the actor is always a real person, and this
    /// says which credential they were reached through.
    /// </summary>
    Guid? ApiKeyId { get; }

    /// <summary>The authenticated user's id, or <see cref="UnauthorizedAccessException"/> if there is none.</summary>
    Guid RequireUserId();
}

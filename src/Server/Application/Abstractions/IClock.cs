namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// Every read of "now" goes through this. Non-negotiable: the occurrence engine is only testable
/// if time is injectable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

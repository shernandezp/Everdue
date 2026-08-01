namespace Everdue.Server.Api;

/// <summary>
/// A configuration combination that is valid but will not work in production.
///
/// These are logged once at startup rather than refused, because refusing to start over a setting
/// that is fine on a developer's machine is worse than the mistake. The point is that the operator
/// reads it before the first user does.
/// </summary>
public interface IStartupWarning
{
    string Message { get; }
}

public sealed record StartupWarning(string Message) : IStartupWarning;

namespace Everdue.Server.Application.Common;

/// <summary>
/// Base for every expected failure. The API layer turns these into RFC 7807 ProblemDetails; the
/// messages are developer-facing English by convention (UI text is localized client-side).
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }

    public abstract string ErrorCode { get; }
}

public sealed class NotFoundException(string resource, object key)
    : AppException($"{resource} '{key}' was not found.")
{
    public override int StatusCode => 404;

    public override string ErrorCode => "not_found";
}

public sealed class ValidationException : AppException
{
    public ValidationException(string message)
        : base(message) => Errors = new Dictionary<string, string[]> { ["request"] = [message] };

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") => Errors = errors;

    public IDictionary<string, string[]> Errors { get; }

    public override int StatusCode => 400;

    public override string ErrorCode => "validation_failed";
}

/// <summary>Used for every rejected status transition — the board relies on the message to explain the refusal.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;

    public override string ErrorCode => "conflict";
}

public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;

    public override string ErrorCode => "forbidden";
}

public sealed class UnauthenticatedException(string message = "Authentication is required.") : AppException(message)
{
    public override int StatusCode => 401;

    public override string ErrorCode => "unauthenticated";
}

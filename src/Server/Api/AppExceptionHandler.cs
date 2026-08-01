using Everdue.Server.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Everdue.Server.Api;

/// <summary>
/// Every expected failure leaves the Application layer as an <see cref="AppException"/> and arrives
/// here as RFC 7807 ProblemDetails. Messages stay English: they are for whoever is reading the
/// network tab, while everything a user sees is localized in the SPA.
/// </summary>
public sealed class AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
            return false;
        }

        httpContext.Response.StatusCode = appException.StatusCode;

        var problem = new ProblemDetails
        {
            Status = appException.StatusCode,
            Title = TitleFor(appException),
            Detail = appException.Message,
            Type = $"https://everdue.app/problems/{appException.ErrorCode}",
        };

        problem.Extensions["code"] = appException.ErrorCode;

        if (appException is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static string TitleFor(AppException exception) => exception switch
    {
        ValidationException => "Validation failed",
        NotFoundException => "Not found",
        ConflictException => "Conflict",
        ForbiddenException => "Forbidden",
        UnauthenticatedException => "Unauthorized",
        _ => "Request failed",
    };
}

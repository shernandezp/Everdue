using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using ValidationException = Everdue.Server.Application.Common.ValidationException;

namespace Everdue.Server.Application.Behaviors;

/// <summary>
/// Runs the data annotations declared on every command/query before its handler sees it. Rules that
/// need the database (uniqueness, transition legality, period bounds) stay in the handlers, where
/// they can be expressed honestly.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> HandleAsync(TRequest input, Func<Task<TResponse>> next, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ValidationException("Request body is required.");
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => r.MemberNames.DefaultIfEmpty("request").Select(m => (Member: m, r.ErrorMessage)))
                .GroupBy(x => x.Member)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray());

            throw new ValidationException(errors);
        }

        return next();
    }
}

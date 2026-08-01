using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.SavedViews;

public sealed class ListSavedViewsHandler(IEverdueDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListSavedViewsQuery, IReadOnlyList<SavedViewDto>>
{
    public async Task<IReadOnlyList<SavedViewDto>> Handle(ListSavedViewsQuery request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        return await db.SavedViews.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderBy(v => v.Name)
            .Select(v => new SavedViewDto(v.Id, v.Name, v.Route, v.QueryString, v.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }
}

public sealed class SaveSavedViewHandler(IEverdueDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<SaveSavedViewCommand, SavedViewDto>
{
    private const int MaxPerUser = 30;

    public async Task<SavedViewDto> Handle(SaveSavedViewCommand request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["name"] = ["A name is required."] });
        }

        if (!SavedViewRoutes.IsSupported(request.Route))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["route"] = [$"Supported routes are: {string.Join(", ", SavedViewRoutes.Supported)}."],
            });
        }

        // The query string is handed straight back to the router, so it never grows a parser here —
        // which is also why a filter added in a later version works in an old saved view for free.
        var queryString = request.QueryString.TrimStart('?');
        if (queryString.Length > 1000)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["queryString"] = ["The filter set is too long."] });
        }

        var userId = currentUser.RequireUserId();
        var existing = await db.SavedViews.FirstOrDefaultAsync(v => v.UserId == userId && v.Name == name, cancellationToken);

        if (existing is null)
        {
            var count = await db.SavedViews.CountAsync(v => v.UserId == userId, cancellationToken);
            if (count >= MaxPerUser)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["name"] = [$"You already have {MaxPerUser} saved views. Delete one first."],
                });
            }

            existing = new SavedView
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Name = name,
                CreatedAt = clock.UtcNow,
            };

            db.SavedViews.Add(existing);
        }

        existing.Route = request.Route.ToLowerInvariant();
        existing.QueryString = queryString;

        await db.SaveChangesAsync(cancellationToken);

        return new SavedViewDto(existing.Id, existing.Name, existing.Route, existing.QueryString, existing.CreatedAt);
    }
}

public sealed class DeleteSavedViewHandler(IEverdueDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteSavedViewCommand, bool>
{
    public async Task<bool> Handle(DeleteSavedViewCommand request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == request.Id && v.UserId == userId, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.SavedView, request.Id);

        db.SavedViews.Remove(view);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

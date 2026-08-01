using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Identity;

public sealed class TelegramLinkStore(EverdueDbContext db) : ITelegramLinkStore
{
    public async Task IssueAsync(
        Guid userId,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var user = await Require(userId, cancellationToken);

        // One live code per person: asking for a new one invalidates the old, so a code left on a
        // screen an hour ago cannot still be used by whoever walks past it.
        user.TelegramLinkCode = code;
        user.TelegramLinkCodeExpiresAt = expiresAt;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlinkAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await Require(userId, cancellationToken);

        user.TelegramChatId = null;
        user.TelegramLinkCode = null;
        user.TelegramLinkCodeExpiresAt = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppUser> Require(Guid userId, CancellationToken cancellationToken)
        => await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
           ?? throw new NotFoundException(ResourceNames.User, userId);
}

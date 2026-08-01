using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>
/// Reads the delivery side of a user row. Kept apart from <see cref="UserDirectory"/> because the
/// rest of the application has no reason to see anybody's phone number or chat id.
/// </summary>
public sealed class NotificationRecipients(EverdueDbContext db, ITenantProvider tenants) : INotificationRecipients
{
    /// <summary>The columns that matter here — projected in SQL so the preference JSON is parsed once, in memory.</summary>
    private sealed record Row(
        Guid Id,
        string DisplayName,
        string? Email,
        string? PreferredLanguage,
        string? NotificationPreferencesJson,
        long? TelegramChatId,
        string? WhatsAppPhoneE164,
        bool Active);

    public async Task<NotificationRecipient?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantDefault = (await tenants.GetAsync(cancellationToken)).DefaultLanguage;

        var row = await Project(db.Users.AsNoTracking().Where(u => u.Id == userId))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToRecipient(row, tenantDefault);
    }

    public async Task<IReadOnlyDictionary<Guid, NotificationRecipient>> MapAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, NotificationRecipient>();
        }

        var tenantDefault = (await tenants.GetAsync(cancellationToken)).DefaultLanguage;

        var rows = await Project(db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => ToRecipient(r, tenantDefault));
    }

    public async Task SavePreferencesAsync(
        Guid userId,
        NotificationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.User, userId);

        user.NotificationPreferencesJson = preferences.ToJson();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Row> Project(IQueryable<AppUser> query)
        => query.Select(u => new Row(
            u.Id,
            u.DisplayName,
            u.Email,
            u.PreferredLanguage,
            u.NotificationPreferencesJson,
            u.TelegramChatId,
            u.WhatsAppPhoneE164,
            u.Active));

    private static NotificationRecipient ToRecipient(Row row, string tenantDefault)
        => new(
            row.Id,
            row.DisplayName,
            row.Email,
            Languages.Resolve(row.PreferredLanguage, tenantDefault),
            NotificationPreferences.Parse(row.NotificationPreferencesJson),
            row.TelegramChatId,
            row.WhatsAppPhoneE164,
            row.Active);
}

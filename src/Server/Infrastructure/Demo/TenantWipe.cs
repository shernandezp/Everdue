using Everdue.Server.Application.Abstractions;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Demo;

/// <summary>
/// Deletes everything this tenant owns, in the one order the foreign keys permit.
///
/// <para>Every reference to a user is <c>Restrict</c> — a person who did something can never be deleted out
/// from under the record — so the accounts can only go once the record they appear in has gone. That is why
/// the order below reads from the leaves inwards and why users are last.</para>
///
/// <para>The delete is a bulk <c>ExecuteDelete</c> per table rather than a load-and-remove: six months of
/// demo history is tens of thousands of rows and loading them into a change tracker to throw them away is
/// pointless. It is still LINQ, so both providers are served by one expression.</para>
///
/// <para><strong>This class has no guard of its own.</strong> Whether the caller is allowed to do this is
/// decided in the handler, once, where the confirmation is checked.</para>
///
/// <para><strong>It clears the change tracker on the way out</strong>, so any entity the request had loaded
/// before the wipe is detached rather than left pointing at a row that no longer exists. Nothing tracked today
/// is deleted here — the caller's own account and the tenant both survive — but the day some middleware loads
/// one that is, the symptom would be a <c>DbUpdateConcurrencyException</c> thrown *after* the delete committed,
/// which is the worst possible moment to discover it. Callers must therefore re-read anything they still need.</para>
/// </summary>
public sealed class TenantWipe(
    EverdueDbContext db,
    IFileStore files,
    ITenantContext tenantContext,
    ILogger<TenantWipe> logger)
{
    public async Task<DemoResetSummary> ExecuteAsync(Guid keepUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;

        // Read the storage keys before the rows go: the bytes live outside the database, and a record of
        // where they were is the only way to find them.
        var storageKeys = await db.Attachments.Select(a => a.StorageKey).ToListAsync(cancellationToken);

        var counts = new
        {
            WorkItems = await db.WorkItems.CountAsync(cancellationToken),
            Responsibilities = await db.Responsibilities.CountAsync(cancellationToken),
            Entities = await db.Entities.CountAsync(cancellationToken),
            Departments = await db.Departments.CountAsync(cancellationToken),
            Notifications = await db.Notifications.CountAsync(cancellationToken),
        };

        // One transaction: a wipe that stops half way leaves a database no screen can render and no
        // migration can repair. Either the tenant is empty or nothing happened.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Leaves first.
        await db.ChecklistItems.ExecuteDeleteAsync(cancellationToken);
        await db.Comments.ExecuteDeleteAsync(cancellationToken);
        await db.Attachments.ExecuteDeleteAsync(cancellationToken);
        await db.WorkItemEvents.ExecuteDeleteAsync(cancellationToken);
        await db.NotificationDeliveries.ExecuteDeleteAsync(cancellationToken);
        await db.Notifications.ExecuteDeleteAsync(cancellationToken);
        await db.WebhookDeliveries.ExecuteDeleteAsync(cancellationToken);

        // The ledger, then what defines it.
        await db.WorkItems.ExecuteDeleteAsync(cancellationToken);
        await db.ChecklistTemplateItems.ExecuteDeleteAsync(cancellationToken);
        await db.Responsibilities.ExecuteDeleteAsync(cancellationToken);

        await db.EntityFieldDefs.ExecuteDeleteAsync(cancellationToken);
        await db.Entities.ExecuteDeleteAsync(cancellationToken);
        await db.Departments.ExecuteDeleteAsync(cancellationToken);

        // Per-user configuration and credentials.
        await db.SavedViews.ExecuteDeleteAsync(cancellationToken);
        await db.DigestSubscriptions.ExecuteDeleteAsync(cancellationToken);
        await db.WebhookSubscriptions.ExecuteDeleteAsync(cancellationToken);
        await db.ApiKeys.ExecuteDeleteAsync(cancellationToken);

        // Channel credentials for THIS tenant. The system-scope row (TenantId = Guid.Empty) belongs to the
        // operator, not the tenant, and survives — it is what a fresh install is meant to keep working with.
        // This table sits outside the global filter, so the predicate has to be written out.
        await db.ChannelSettings.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);

        // Identity last, and never the administrator doing this: an install nobody can sign into is not a
        // reset, it is a brick.
        //
        // Identity's own side tables are deleted explicitly rather than left to cascade. UserLogins is the one
        // that matters: a Google account linked to a deleted user would otherwise keep a row pointing at
        // nobody, and the next sign-in with that Google account resolves to it.
        var doomed = await db.Users
            .Where(u => u.Id != keepUserId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        await db.UserTokens.Where(t => doomed.Contains(t.UserId)).ExecuteDeleteAsync(cancellationToken);
        await db.UserLogins.Where(l => doomed.Contains(l.UserId)).ExecuteDeleteAsync(cancellationToken);
        await db.UserClaims.Where(c => doomed.Contains(c.UserId)).ExecuteDeleteAsync(cancellationToken);

        var deletedUsers = await db.Users.Where(u => u.Id != keepUserId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // See the class comment: ExecuteDelete does not touch the change tracker, so anything the request had
        // already loaded is now a tracked ghost.
        db.ChangeTracker.Clear();

        // Only once the rows are certainly gone. The other order can delete a photograph and then fail to
        // delete its record, which is the one outcome that produces a broken download rather than a clean one.
        var deletedFiles = 0;

        foreach (var key in storageKeys)
        {
            try
            {
                await files.DeleteAsync(key, cancellationToken);
                deletedFiles++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A locked, read-only or already-missing file must not fail a wipe that has already committed:
                // throwing here would report failure for an operation that did happen, and the administrator
                // would run it again. Loud, not fatal. UnauthorizedAccessException is not an IOException.
                logger.LogWarning(exception, "Could not delete attachment bytes '{Key}' during a tenant wipe.", key);
            }
        }

        logger.LogWarning(
            "Tenant {TenantId} was WIPED by user {UserId}: {WorkItems} work items, {Responsibilities} responsibilities, " +
            "{Entities} entities, {Departments} departments, {Notifications} notifications, {Users} user accounts and " +
            "{Files} attachment files were deleted. This is irreversible.",
            tenantId,
            keepUserId,
            counts.WorkItems,
            counts.Responsibilities,
            counts.Entities,
            counts.Departments,
            counts.Notifications,
            deletedUsers,
            deletedFiles);

        return new DemoResetSummary(
            counts.WorkItems,
            counts.Responsibilities,
            counts.Entities,
            counts.Departments,
            deletedUsers,
            deletedFiles,
            counts.Notifications);
    }
}

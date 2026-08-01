using Everdue.Server.Application.Abstractions;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Support;

public static class LedgerSeedExtensions
{
    /// <summary>
    /// Writes ledger history through the DbContext, in the tenant's own time zone and against the test
    /// clock, and saves it. The callback receives the builder and the administrator's id, which is the
    /// default owner for anything that does not name one.
    /// </summary>
    public static Task SeedAsync(this EverdueApp app, Action<LedgerBuilder, Guid> seed)
        => app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var timeZone = await services.GetRequiredService<ITenantProvider>().GetTimeZoneAsync();
            var owner = await db.Users.Where(u => u.Email == EverdueApp.AdminEmail).Select(u => u.Id).SingleAsync();

            var ledger = new LedgerBuilder(db, timeZone, app.Clock.UtcNow);
            seed(ledger, owner);
            await ledger.SaveAsync();
        });
}

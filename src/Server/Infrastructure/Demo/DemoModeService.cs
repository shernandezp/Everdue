using Everdue.Server.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Demo;

/// <summary>
/// The Application layer's one door to demo mode: the policy question ("may this install be reset at all?")
/// and the two operations, which are otherwise two unrelated classes with no reason to know about each other.
///
/// It composes rather than implements, because the wipe is useful on its own — turning demo mode *off* is a
/// wipe with no seed after it.
/// </summary>
public sealed class DemoModeService(
    TenantWipe wipe,
    DemoDataSeeder seeder,
    IOptions<DemoOptions> options) : IDemoMode
{
    public bool ResetAllowed => options.Value.AllowReset;

    public string DemoPassword => options.Value.Password;

    public Task<DemoResetSummary> WipeAsync(Guid keepUserId, CancellationToken cancellationToken = default)
        => wipe.ExecuteAsync(keepUserId, cancellationToken);

    public Task<DemoSeedSummary> SeedAsync(CancellationToken cancellationToken = default)
        => seeder.SeedNowAsync(cancellationToken);
}

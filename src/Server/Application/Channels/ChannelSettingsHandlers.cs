using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Channels;

public sealed class ListChannelSettingsHandler(
    IChannelSettingsResolver resolver,
    IChannelRegistry channels,
    ITenantContext tenantContext) : IRequestHandler<ListChannelSettingsQuery, IReadOnlyList<ChannelSettingsDto>>
{
    public async Task<IReadOnlyList<ChannelSettingsDto>> Handle(
        ListChannelSettingsQuery request,
        CancellationToken cancellationToken = default)
    {
        // "Configured" is asked of the channel, not of this table: e-mail can also be configured by
        // v1's appsettings block, and a screen that only read rows would tell an install that has
        // been sending mail for months that e-mail is not set up.
        var configured = await channels.ConfiguredAsync(cancellationToken);

        var result = new List<ChannelSettingsDto>();

        foreach (var channel in Enum.GetValues<NotificationChannel>())
        {
            var own = await resolver.ReadScopeAsync(tenantContext.TenantId, channel, cancellationToken);
            var effective = own ?? await resolver.ResolveAsync(channel, cancellationToken);

            result.Add(new ChannelSettingsDto(
                channel,
                Configured: configured.Contains(channel),

                // The same answer, deliberately: an inactive row does not resolve, so "configured"
                // already means "would send". Reporting them separately invited a screen that says
                // configured and not active at the same time, which describes nothing real.
                Active: configured.Contains(channel),
                UsingSystemScope: own is null && configured.Contains(channel),
                Summary: effective is null ? null : ChannelConfigView.Describe(channel, effective.ConfigJson),

                // Only this tenant's own row is editable, so only that one comes back.
                RedactedConfigJson: own is null ? null : ChannelConfigView.Redact(own.ConfigJson),
                UpdatedAt: null));
        }

        return result;
    }
}

/// <summary>
/// Health, derived from the delivery rows rather than a counter column: the table already knows, and
/// a counter is one more thing that can disagree with reality.
/// </summary>
public sealed class ChannelHealthHandler(IEverdueDbContext db, IChannelRegistry channels, IClock clock)
    : IRequestHandler<ChannelHealthQuery, IReadOnlyList<ChannelHealthDto>>
{
    public async Task<IReadOnlyList<ChannelHealthDto>> Handle(
        ChannelHealthQuery request,
        CancellationToken cancellationToken = default)
    {
        var since = clock.UtcNow.AddHours(-24);
        var configured = await channels.ConfiguredAsync(cancellationToken);

        var rows = await db.NotificationDeliveries.AsNoTracking()
            .Where(d => d.Status == DeliveryStatus.Pending || d.NextAttemptAt >= since)
            .GroupBy(d => new { d.Channel, d.Status })
            .Select(g => new StatusCount(g.Key.Channel, g.Key.Status, g.Count()))
            .ToListAsync(cancellationToken);

        var result = new List<ChannelHealthDto>();

        foreach (var channel in Enum.GetValues<NotificationChannel>())
        {
            var last = await db.NotificationDeliveries.AsNoTracking()
                .Where(d => d.Channel == channel && d.Status == DeliveryStatus.Failed && d.LastError != null)
                .OrderByDescending(d => d.NextAttemptAt)
                .Select(d => new { d.LastError, d.NextAttemptAt })
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new ChannelHealthDto(
                channel,
                Configured: configured.Contains(channel),
                Pending: Count(rows, channel, DeliveryStatus.Pending),
                FailedRecently: Count(rows, channel, DeliveryStatus.Failed),
                SkippedRecently: Count(rows, channel, DeliveryStatus.Skipped),
                LastError: last?.LastError,
                LastErrorAt: last?.NextAttemptAt,

                // Stated rather than implied: without a public webhook WhatsApp gives no delivery or
                // read callbacks, so "sent" there means "Meta accepted it", not "it reached a phone".
                DeliveryReceiptsSupported: channel != NotificationChannel.WhatsApp));
        }

        return result;

        static int Count(IReadOnlyList<StatusCount> rows, NotificationChannel channel, DeliveryStatus status)
            => rows.Where(r => r.Channel == channel && r.Status == status).Sum(r => r.Count);
    }

    private sealed record StatusCount(NotificationChannel Channel, DeliveryStatus Status, int Count);
}

public sealed class SaveChannelSettingsHandler(
    IChannelSettingsResolver resolver,
    ITenantContext tenantContext) : IRequestHandler<SaveChannelSettingsCommand, ChannelSettingsDto>
{
    public async Task<ChannelSettingsDto> Handle(SaveChannelSettingsCommand request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            throw new ValidationException("A channel configuration is required.");
        }

        var scope = tenantContext.TenantId;
        var merged = await MergeSecretsAsync(scope, request, cancellationToken);

        // Refused before it is stored: saving a configuration that can never send would leave the
        // screen reporting "not configured" with nothing to say about which field is missing.
        ChannelConfigView.EnsureUsable(request.Channel, merged);

        await resolver.SaveAsync(scope, request.Channel, merged, request.Active, cancellationToken);

        var stored = await resolver.ReadScopeAsync(scope, request.Channel, cancellationToken);

        return new ChannelSettingsDto(
            request.Channel,
            Configured: stored is not null,
            Active: request.Active,
            UsingSystemScope: false,
            Summary: stored is null ? null : ChannelConfigView.Describe(request.Channel, stored.ConfigJson),
            RedactedConfigJson: stored is null ? null : ChannelConfigView.Redact(stored.ConfigJson),
            UpdatedAt: null);
    }

    /// <summary>
    /// A secret the screen could not show cannot be re-typed on every edit, so a blank one means
    /// "keep what is stored". Without this, changing a bot's username would silently wipe its token.
    /// </summary>
    private async Task<string> MergeSecretsAsync(Guid scope, SaveChannelSettingsCommand request, CancellationToken cancellationToken)
    {
        var existing = await resolver.ReadScopeAsync(scope, request.Channel, cancellationToken);
        if (existing is null)
        {
            return request.ConfigJson;
        }

        return request.Channel switch
        {
            NotificationChannel.Email => Merge<SmtpChannelConfig>(request.ConfigJson, existing.ConfigJson, (incoming, stored) =>
            {
                if (string.IsNullOrWhiteSpace(incoming.Password))
                {
                    incoming.Password = stored.Password;
                }
            }),

            NotificationChannel.Telegram => Merge<TelegramChannelConfig>(request.ConfigJson, existing.ConfigJson, (incoming, stored) =>
            {
                if (string.IsNullOrWhiteSpace(incoming.BotToken))
                {
                    incoming.BotToken = stored.BotToken;
                }
            }),

            NotificationChannel.WhatsApp => Merge<WhatsAppChannelConfig>(request.ConfigJson, existing.ConfigJson, (incoming, stored) =>
            {
                if (string.IsNullOrWhiteSpace(incoming.AccessToken))
                {
                    incoming.AccessToken = stored.AccessToken;
                }
            }),

            _ => request.ConfigJson,
        };
    }

    private static string Merge<T>(string incomingJson, string storedJson, Action<T, T> carryOver)
        where T : class
    {
        var incoming = ChannelConfigJson.Deserialize<T>(incomingJson);
        var stored = ChannelConfigJson.Deserialize<T>(storedJson);

        if (incoming is null)
        {
            throw new ValidationException("The channel configuration could not be read.");
        }

        if (stored is not null)
        {
            carryOver(incoming, stored);
        }

        return ChannelConfigJson.Serialize(incoming);
    }
}

public sealed class DeleteChannelSettingsHandler(IChannelSettingsResolver resolver, ITenantContext tenantContext)
    : IRequestHandler<DeleteChannelSettingsCommand, bool>
{
    public async Task<bool> Handle(DeleteChannelSettingsCommand request, CancellationToken cancellationToken = default)
    {
        await resolver.DeleteAsync(tenantContext.TenantId, request.Channel, cancellationToken);
        return true;
    }
}

/// <summary>Sends one dull message to the administrator asking, which is the only honest way to test a channel.</summary>
public sealed class TestChannelHandler(
    IChannelRegistry registry,
    INotificationRecipients recipients,
    ICurrentUser currentUser) : IRequestHandler<TestChannelCommand, ChannelTestResultDto>
{
    public async Task<ChannelTestResultDto> Handle(TestChannelCommand request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var person = await recipients.FindAsync(userId, cancellationToken) ?? throw new NotFoundException(ResourceNames.User, userId);

        var channel = registry.Find(request.Channel);
        if (channel is null)
        {
            return new ChannelTestResultDto(false, $"No implementation for channel {request.Channel}.");
        }

        if (!person.CanReceiveOn(request.Channel))
        {
            return new ChannelTestResultDto(false, $"You have no {request.Channel} address on your profile.");
        }

        var result = await channel.SendAsync(
            person.ToChannelRecipient(),
            NotificationTemplates.RenderTest(person.Language),
            cancellationToken);

        return new ChannelTestResultDto(result.Outcome == ChannelSendOutcome.Sent, result.Error);
    }
}

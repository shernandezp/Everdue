using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Localization;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Channels;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

/// <summary>
/// The only thing Everdue needs to *receive*: a user proving which Telegram chat is theirs.
///
/// Long polling rather than a webhook. A self-hosted install usually sits behind NAT with no inbound
/// HTTPS endpoint, and requiring one would make the channel unusable for exactly the audience it
/// exists for. The offset lives in memory: Telegram redelivers unconfirmed updates for 24 hours and
/// re-processing a <c>/start</c> is idempotent, so a restart costs nothing.
/// </summary>
public sealed class TelegramUpdatePollingService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    TelegramApiClient api,
    IOptions<TelegramOptions> options,
    ILogger<TelegramUpdatePollingService> logger) : BackgroundService
{
    private long _offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.PollingEnabled)
        {
            logger.LogInformation("Telegram polling is disabled (Telegram:PollingEnabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var outcome = await PollOnceAsync(stoppingToken);

                // A successful long poll already blocked for its timeout, so it loops straight back.
                // The other two outcomes return immediately and must not be allowed to spin: an
                // unconfigured channel would busy-loop on a token that does not exist, and a network
                // outage would hammer a dead connection as fast as the socket can refuse it.
                var pause = outcome switch
                {
                    PollOutcome.NotConfigured => TimeSpan.FromMinutes(1),
                    PollOutcome.Unreachable => TimeSpan.FromSeconds(30),
                    _ => TimeSpan.Zero,
                };

                if (pause > TimeSpan.Zero)
                {
                    await Task.Delay(pause, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Telegram polling failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    public enum PollOutcome
    {
        /// <summary>Reached Telegram. Whether anything came back is not the caller's concern.</summary>
        Polled = 0,

        /// <summary>No bot token resolves. Nothing to poll, and nothing wrong.</summary>
        NotConfigured = 1,

        /// <summary>The call did not reach Telegram, so it returned instantly rather than blocking.</summary>
        Unreachable = 2,
    }

    /// <summary>One pass. Exposed so tests drive it directly instead of racing the loop.</summary>
    public async Task<PollOutcome> PollOnceAsync(CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved)
        {
            return PollOutcome.NotConfigured;
        }

        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        var config = (await services.GetRequiredService<IChannelSettingsResolver>()
                .ResolveAsync(NotificationChannel.Telegram, cancellationToken))
            ?.Read<TelegramChannelConfig>();

        if (config is not { IsUsable: true })
        {
            return PollOutcome.NotConfigured;
        }

        var result = await api.GetUpdatesAsync(
            config.BotToken!,
            _offset,
            options.Value.PollTimeoutSeconds,
            cancellationToken);

        if (!result.Ok)
        {
            return PollOutcome.Unreachable;
        }

        foreach (var update in result.Updates)
        {
            _offset = Math.Max(_offset, update.UpdateId + 1);

            if (StartPayload(update.Message?.Text) is { } code)
            {
                await LinkAsync(services, config, update.Message!.Chat.Id, code, cancellationToken);
            }
        }

        return PollOutcome.Polled;
    }

    /// <summary>The deep link the user tapped arrives as the text "/start &lt;code&gt;".</summary>
    private static string? StartPayload(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[1] : null;
    }

    private async Task LinkAsync(
        IServiceProvider services,
        TelegramChannelConfig config,
        long chatId,
        string code,
        CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var tenant = await services.GetRequiredService<ITenantProvider>().GetAsync(cancellationToken);

        var normalized = code.Trim().ToUpperInvariant();
        var now = clock.UtcNow;

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.TelegramLinkCode == normalized && u.TelegramLinkCodeExpiresAt > now && u.Active,
            cancellationToken);

        if (user is null)
        {
            // Never say whether the code was wrong or merely expired: this is an unauthenticated
            // surface, and a bot that confirms code shapes is a bot that can be probed.
            await api.SendMessageAsync(config.BotToken!, chatId, TelegramLinkMessages.Failed(tenant.DefaultLanguage), cancellationToken);
            return;
        }

        user.TelegramChatId = chatId;
        user.TelegramLinkCode = null;
        user.TelegramLinkCodeExpiresAt = null;

        // Linking a channel is the moment somebody asks to be reached on it — anything else would
        // mean going through the whole flow and still receiving nothing.
        var preferences = NotificationPreferences.Parse(user.NotificationPreferencesJson);
        preferences.Channel ??= NotificationChannel.Telegram;
        user.NotificationPreferencesJson = preferences.ToJson();

        await db.SaveChangesAsync(cancellationToken);

        var language = Languages.Resolve(user.PreferredLanguage, tenant.DefaultLanguage);
        await api.SendMessageAsync(config.BotToken!, chatId, TelegramLinkMessages.Linked(language, user.DisplayName), cancellationToken);

        logger.LogInformation("Linked Telegram chat to user {UserId}.", user.Id);
    }
}

/// <summary>
/// The two sentences the bot ever says. The wording lives in <c>Resources/BotStrings*.resx</c>; this
/// is here so nothing else has to know which keys the bot uses.
/// </summary>
internal static class TelegramLinkMessages
{
    public static string Linked(string language, string displayName)
        => AppText.Bot.Format(language, BotText.Linked, displayName);

    public static string Failed(string language)
        => AppText.Bot[language, BotText.LinkFailed];
}

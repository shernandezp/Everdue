using System.Net;
using System.Text;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Engine;
using Everdue.Server.Infrastructure.Channels;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Everdue.Server.Tests.Notifications;

/// <summary>
/// Answers getUpdates with whatever the test queued and records what the bot said back. No live
/// Telegram is ever contacted.
/// </summary>
internal sealed class TelegramStubHandler : HttpMessageHandler
{
    private readonly Queue<string> _updates = new();

    public List<string> SentMessages { get; } = [];

    public void QueueStart(long chatId, string code)
        => _updates.Enqueue(
            """{"ok":true,"result":[{"update_id":1,"message":{"chat":{"id":CHAT},"text":"/start CODE"}}]}"""
                .Replace("CHAT", chatId.ToString())
                .Replace("CODE", code));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/getUpdates", StringComparison.Ordinal))
        {
            var body = _updates.Count > 0 ? _updates.Dequeue() : """{"ok":true,"result":[]}""";
            return Json(body);
        }

        if (request.Content is not null)
        {
            SentMessages.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return Json("""{"ok":true,"result":{}}""");
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

/// <summary>
/// The link flow, which is the only thing Everdue needs to *receive*. Long polling, so a self-hosted
/// install behind a router can use the channel at all.
/// </summary>
public class TelegramLinkTests
{
    private const string BotConfig = """{"botToken":"BOT-TOKEN","botUsername":"everduebot"}""";

    private static async Task<(EverdueApp App, TelegramStubHandler Handler)> StartAsync()
    {
        var handler = new TelegramStubHandler();

        var app = await EverdueApp.StartWithServicesAsync(TestProvider.Sqlite, services =>
        {
            services.RemoveAll<TelegramApiClient>();
            services.AddSingleton(new TelegramApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org/") }));
        });

        await app.ConfigureChannelAsync(NotificationChannel.Telegram, BotConfig);
        return (app, handler);
    }

    [Fact]
    public async Task A_start_command_with_a_valid_code_links_the_chat_and_selects_the_channel()
    {
        var (app, handler) = await StartAsync();
        await using var _ = app;

        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var link = await member.PostJsonAsync<TelegramLinkDto>("/api/v1/me/telegram/link");

        link.Code.Length.ShouldBe(8);
        link.DeepLink.ShouldBe($"https://t.me/everduebot?start={link.Code}");
        link.ExpiresAt.ShouldBeGreaterThan(app.Clock.UtcNow);

        handler.QueueStart(chatId: 55501, code: link.Code);
        (await app.Services.GetRequiredService<TelegramUpdatePollingService>().PollOnceAsync(CancellationToken.None))
            .ShouldBe(TelegramUpdatePollingService.PollOutcome.Polled);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == memberId);

            user.TelegramChatId.ShouldBe(55501);

            // Single use: the code is spent.
            user.TelegramLinkCode.ShouldBeNull();

            // Linking a channel is the moment somebody asks to be reached on it.
            NotificationPreferences.Parse(user.NotificationPreferencesJson).Channel.ShouldBe(NotificationChannel.Telegram);
        });

        handler.SentMessages.ShouldNotBeEmpty();
    }

    /// <summary>
    /// An unknown or expired code says the same thing either way: this is an unauthenticated surface,
    /// and a bot that confirms code shapes is a bot that can be probed.
    /// </summary>
    [Fact]
    public async Task An_expired_code_does_not_link()
    {
        var (app, handler) = await StartAsync();
        await using var _ = app;

        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var link = await member.PostJsonAsync<TelegramLinkDto>("/api/v1/me/telegram/link");

        app.Clock.Set(link.ExpiresAt.AddMinutes(1));

        handler.QueueStart(chatId: 55502, code: link.Code);
        await app.Services.GetRequiredService<TelegramUpdatePollingService>().PollOnceAsync(CancellationToken.None);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            (await db.Users.SingleAsync(u => u.Id == memberId)).TelegramChatId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task An_unknown_code_does_not_link()
    {
        var (app, handler) = await StartAsync();
        await using var _ = app;

        handler.QueueStart(chatId: 55503, code: "ZZZZZZZZ");
        await app.Services.GetRequiredService<TelegramUpdatePollingService>().PollOnceAsync(CancellationToken.None);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            (await db.Users.CountAsync(u => u.TelegramChatId != null)).ShouldBe(0);
        });
    }

    /// <summary>Asking to link when the installation has no bot is a refusal, not a dead code.</summary>
    [Fact]
    public async Task Linking_is_refused_when_telegram_is_not_configured()
    {
        await using var app = await EverdueApp.StartAsync(TestProvider.Sqlite);
        var member = await app.SignInAsMemberAsync();

        var response = await member.PostJsonAsync("/api/v1/me/telegram/link");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unlinking_clears_the_chat_and_the_channel_choice()
    {
        var (app, handler) = await StartAsync();
        await using var _ = app;

        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var link = await member.PostJsonAsync<TelegramLinkDto>("/api/v1/me/telegram/link");
        handler.QueueStart(chatId: 55504, code: link.Code);
        await app.Services.GetRequiredService<TelegramUpdatePollingService>().PollOnceAsync(CancellationToken.None);

        var preferences = await member.DeleteFromJsonAsync<NotificationPreferencesDto>("/api/v1/me/telegram/link");

        preferences.TelegramLinked.ShouldBeFalse();
        preferences.Channel.ShouldBeNull();

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            (await db.Users.SingleAsync(u => u.Id == memberId)).TelegramChatId.ShouldBeNull();
        });
    }
}

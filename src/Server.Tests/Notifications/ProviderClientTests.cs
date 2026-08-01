using System.Net;
using System.Text;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Channels;

namespace Everdue.Server.Tests.Notifications;

/// <summary>
/// Answers whatever the test says, and remembers what was asked. No live provider is ever called
/// from the suite: what matters here is that a provider's "no" is classified correctly.
/// </summary>
internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.ToString());

        if (request.Content is not null)
        {
            Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}

public class TelegramApiClientTests
{
    private static (TelegramApiClient Client, StubHandler Handler) Build(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org/") };
        return (new TelegramApiClient(http), handler);
    }

    [Fact]
    public async Task A_successful_send_calls_sendMessage_with_the_chat_and_the_text()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"ok":true,"result":{}}""");

        var result = await client.SendMessageAsync("BOT-TOKEN", 4242, "Missed: Inventory check");

        result.Ok.ShouldBeTrue();
        handler.Requests.Single().ShouldEndWith("botBOT-TOKEN/sendMessage");
        handler.Bodies.Single().ShouldContain("4242");
        handler.Bodies.Single().ShouldContain("Inventory check");
    }

    /// <summary>
    /// 429 carries the exact number of seconds to wait. Honouring it is the difference between
    /// backing off and being rate-limited harder.
    /// </summary>
    [Fact]
    public async Task Too_many_requests_is_retryable_and_honours_retry_after()
    {
        var (client, _) = Build(
            HttpStatusCode.TooManyRequests,
            """{"ok":false,"description":"Too Many Requests","parameters":{"retry_after":17}}""");

        var result = await client.SendMessageAsync("t", 1, "hi");

        result.Ok.ShouldBeFalse();
        result.Retryable.ShouldBeTrue();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(17));
    }

    /// <summary>A revoked token is wrong now and will be wrong in five minutes.</summary>
    [Fact]
    public async Task An_unauthorized_token_is_permanent()
    {
        var (client, _) = Build(HttpStatusCode.Unauthorized, """{"ok":false,"description":"Unauthorized"}""");

        var result = await client.SendMessageAsync("bad", 1, "hi");

        result.Retryable.ShouldBeFalse();
        result.ChatUnreachable.ShouldBeFalse();
    }

    /// <summary>Blocking the bot is a decision. The caller forgets the chat id rather than retrying it.</summary>
    [Fact]
    public async Task A_blocked_bot_marks_the_chat_unreachable()
    {
        var (client, _) = Build(HttpStatusCode.Forbidden, """{"ok":false,"description":"Forbidden: bot was blocked by the user"}""");

        var result = await client.SendMessageAsync("t", 1, "hi");

        result.Retryable.ShouldBeFalse();
        result.ChatUnreachable.ShouldBeTrue();
    }

    [Fact]
    public async Task A_server_error_is_retryable()
    {
        var (client, _) = Build(HttpStatusCode.BadGateway, "{}");

        (await client.SendMessageAsync("t", 1, "hi")).Retryable.ShouldBeTrue();
    }
}

public class WhatsAppCloudApiClientTests
{
    private static (WhatsAppCloudApiClient Client, StubHandler Handler) Build(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        return (new WhatsAppCloudApiClient(http), handler);
    }

    /// <summary>
    /// The request has to be a template message with positional variables — business-initiated free
    /// text is not a thing WhatsApp allows, so there is no other shape to send.
    /// </summary>
    [Fact]
    public async Task A_send_posts_a_template_with_its_positional_variables()
    {
        var (client, handler) = Build(HttpStatusCode.OK, """{"messages":[{"id":"wamid.1"}]}""");

        var result = await client.SendTemplateAsync(
            "PHONE-ID",
            "TOKEN",
            "+573001112233",
            "everdue_missed",
            "es",
            ["Inventory check", "Entidad: Globex", "María"]);

        result.Ok.ShouldBeTrue();
        handler.Requests.Single().ShouldContain("PHONE-ID/messages");

        var body = handler.Bodies.Single();
        body.ShouldContain("everdue_missed");
        body.ShouldContain("\"code\":\"es\"");
        body.ShouldContain("Inventory check");

        // The '+' is stripped: Meta wants the number without it.
        body.ShouldContain("573001112233");
        body.ShouldNotContain("+573001112233");
    }

    /// <summary>
    /// Template faults are configuration mistakes: every message of that type will fail identically,
    /// so they are permanent and worth raising to an administrator rather than retrying quietly.
    /// </summary>
    [Theory]
    [InlineData(132000)]
    [InlineData(132001)]
    public async Task Template_errors_are_permanent_configuration_faults(int code)
    {
        var body = """{"error":{"message":"template problem","code":CODE}}""".Replace("CODE", code.ToString());
        var (client, _) = Build(HttpStatusCode.BadRequest, body);

        var result = await client.SendTemplateAsync("p", "t", "+1", "tpl", "es", ["a"]);

        result.Ok.ShouldBeFalse();
        result.Retryable.ShouldBeFalse();
        result.ConfigurationFault.ShouldBeTrue();
        result.Error.ShouldContain(code.ToString());
    }

    /// <summary>
    /// Meta's deliberate bucket error. The honest reading is "this number may not be reachable",
    /// not "try again in a minute".
    /// </summary>
    [Fact]
    public async Task An_undeliverable_message_is_not_retried()
    {
        var (client, _) = Build(HttpStatusCode.BadRequest, """{"error":{"message":"Message undeliverable","code":131026}}""");

        var result = await client.SendTemplateAsync("p", "t", "+1", "tpl", "es", ["a"]);

        result.Retryable.ShouldBeFalse();
        result.ConfigurationFault.ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task Transport_failures_are_classified(HttpStatusCode status, bool retryable)
    {
        var (client, _) = Build(status, "{}");

        (await client.SendTemplateAsync("p", "t", "+1", "tpl", "es", ["a"])).Retryable.ShouldBe(retryable);
    }
}

/// <summary>Hands back one fixed configuration, so the channel's own decisions are what is under test.</summary>
internal sealed class FixedResolver(string? configJson) : IChannelSettingsResolver
{
    public Task<ResolvedChannelConfig?> ResolveAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
        => Task.FromResult(configJson is null ? null : new ResolvedChannelConfig(channel, Guid.NewGuid(), configJson));

    public Task<bool> CanUseSystemChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ResolvedChannelConfig?> ReadScopeAsync(Guid scope, NotificationChannel channel, CancellationToken cancellationToken = default)
        => ResolveAsync(channel, cancellationToken);

    public Task SaveAsync(Guid scope, NotificationChannel channel, string json, bool active, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(Guid scope, NotificationChannel channel, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Acceptance criterion 5's other half: which template is chosen, and what happens when the approval
/// for one has not landed yet.
/// </summary>
public class WhatsAppChannelTests
{
    private const string Configured = """
        {"phoneNumberId":"PN","accessToken":"TK","templateLanguage":"es","templates":{"Missed":"everdue_missed"}}
        """;

    private static ChannelRecipient Recipient => new(Guid.NewGuid(), "María", null, null, "+573001112233", "es");

    private static (WhatsAppChannel Channel, StubHandler Handler) Build(string? config)
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"messages":[{"id":"wamid.1"}]}""");
        var api = new WhatsAppCloudApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") });

        return (new WhatsAppChannel(new FixedResolver(config), api), handler);
    }

    [Fact]
    public async Task A_mapped_type_sends_its_approved_template()
    {
        var (channel, handler) = Build(Configured);

        var message = new ChannelMessage(
            "Everdue",
            "Missed: Inventory check",
            TemplateKey: "Missed",
            TemplateArgs: ["Inventory check", "Entidad: Globex", "Everdue"]);

        var result = await channel.SendAsync(Recipient, message);

        result.Outcome.ShouldBe(ChannelSendOutcome.Sent);
        handler.Bodies.Single().ShouldContain("everdue_missed");
    }

    /// <summary>
    /// A type whose template is not approved yet leaves the person on their other channel rather than
    /// producing a red row every time something happens.
    /// </summary>
    [Fact]
    public async Task A_type_with_no_configured_template_is_skipped_not_failed()
    {
        var (channel, handler) = Build(Configured);

        var message = new ChannelMessage("Everdue", "text", TemplateKey: "Assigned", TemplateArgs: ["a", "b", "c"]);
        var result = await channel.SendAsync(Recipient, message);

        result.Outcome.ShouldBe(ChannelSendOutcome.Skipped);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unconfigured_channel_is_skipped()
    {
        var (channel, _) = Build(null);

        var message = new ChannelMessage("Everdue", "text", TemplateKey: "Missed", TemplateArgs: ["a", "b", "c"]);
        (await channel.SendAsync(Recipient, message)).Outcome.ShouldBe(ChannelSendOutcome.Skipped);
    }

    [Fact]
    public async Task A_recipient_without_a_number_is_skipped()
    {
        var (channel, _) = Build(Configured);

        var recipient = Recipient with { WhatsAppPhoneE164 = null };
        var message = new ChannelMessage("Everdue", "text", TemplateKey: "Missed", TemplateArgs: ["a", "b", "c"]);

        (await channel.SendAsync(recipient, message)).Outcome.ShouldBe(ChannelSendOutcome.Skipped);
    }
}

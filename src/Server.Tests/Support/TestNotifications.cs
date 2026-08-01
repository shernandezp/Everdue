using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;

namespace Everdue.Server.Tests.Support;

/// <summary>
/// Records what the system tried to tell people, without needing a user row for each of them.
/// Used where the assertion is "did this trigger fire", not "did the row land".
/// </summary>
public sealed class RecordingNotificationEnqueuer : INotificationEnqueuer
{
    public List<NotificationRequest> Requests { get; } = [];

    public Task EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.CompletedTask;
    }

    public Task EnqueueManyAsync(IReadOnlyCollection<NotificationRequest> requests, CancellationToken cancellationToken = default)
    {
        Requests.AddRange(requests);
        return Task.CompletedTask;
    }

    public IReadOnlyList<NotificationRequest> Of(NotificationType type)
        => Requests.Where(r => r.Type == type).ToArray();
}

/// <summary>
/// Reads recipients straight out of the harness database. The production implementation needs a
/// resolved tenant provider; this one only needs the rows.
/// </summary>
public sealed class HarnessNotificationRecipients(EverdueDbContext db, string tenantDefaultLanguage) : INotificationRecipients
{
    public Task<NotificationRecipient?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(All().TryGetValue(userId, out var found) ? found : null);

    public Task<IReadOnlyDictionary<Guid, NotificationRecipient>> MapAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToHashSet();
        var all = All();

        IReadOnlyDictionary<Guid, NotificationRecipient> result = all
            .Where(pair => ids.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return Task.FromResult(result);
    }

    public async Task SavePreferencesAsync(
        Guid userId,
        NotificationPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var user = db.Users.Single(u => u.Id == userId);
        user.NotificationPreferencesJson = preferences.ToJson();
        await db.SaveChangesAsync(cancellationToken);
    }

    private Dictionary<Guid, NotificationRecipient> All()
        => db.Users.ToList().ToDictionary(
            u => u.Id,
            u => new NotificationRecipient(
                u.Id,
                u.DisplayName,
                u.Email,
                Languages.IsSupported(u.PreferredLanguage) ? u.PreferredLanguage! : tenantDefaultLanguage,
                NotificationPreferences.Parse(u.NotificationPreferencesJson),
                u.TelegramChatId,
                u.WhatsAppPhoneE164,
                u.Active));
}

/// <summary>
/// A channel whose answers the test chooses. Queued outcomes are consumed in order and the last one
/// repeats, so "fail twice then succeed" is one line.
/// </summary>
public sealed class TestChannel(NotificationChannel channel = NotificationChannel.Telegram) : INotificationChannel
{
    private readonly Queue<ChannelSendResult> _outcomes = new();

    public NotificationChannel Channel { get; } = channel;

    public List<(ChannelRecipient Recipient, ChannelMessage Message)> Sent { get; } = [];

    public ChannelSendResult Default { get; set; } = ChannelSendResult.Sent();

    /// <summary>A stand-in provider is configured unless a test says otherwise.</summary>
    public bool Configured { get; set; } = true;

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) => Task.FromResult(Configured);

    public TestChannel Then(ChannelSendResult result)
    {
        _outcomes.Enqueue(result);
        return this;
    }

    public Task<ChannelSendResult> SendAsync(
        ChannelRecipient recipient,
        ChannelMessage message,
        CancellationToken cancellationToken = default)
    {
        Sent.Add((recipient, message));
        return Task.FromResult(_outcomes.Count > 0 ? _outcomes.Dequeue() : Default);
    }
}

/// <summary>
/// Answers the two report queries the digest builder asks for with nothing. The digest's own
/// sections are asserted through the real handlers in the API tests; here the point is the three
/// ledger-derived sections, and a stub keeps that test about them.
/// </summary>
public sealed class EmptyReportSender : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        object empty = request switch
        {
            _ when typeof(TResponse) == typeof(IReadOnlyList<Everdue.Server.Application.Contracts.BlockedByEntityGroupDto>)
                => Array.Empty<Everdue.Server.Application.Contracts.BlockedByEntityGroupDto>(),

            _ when typeof(TResponse) == typeof(IReadOnlyList<Everdue.Server.Application.Contracts.NeglectRowDto>)
                => Array.Empty<Everdue.Server.Application.Contracts.NeglectRowDto>(),

            _ => throw new NotSupportedException($"EmptyReportSender does not answer {request.GetType().Name}."),
        };

        return Task.FromResult((TResponse)empty);
    }

    public Task Send(IRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;
}

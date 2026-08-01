using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>
/// Resolves a channel implementation by enum. Adding SMS or Slack later is a registration and
/// nothing else — no caller knows which channels exist.
/// </summary>
public sealed class ChannelRegistry(IEnumerable<INotificationChannel> channels) : IChannelRegistry
{
    private readonly Dictionary<NotificationChannel, INotificationChannel> _byChannel =
        channels.ToDictionary(c => c.Channel);

    public IReadOnlyList<INotificationChannel> All => _byChannel.Values.ToArray();

    public INotificationChannel? Find(NotificationChannel channel)
        => _byChannel.TryGetValue(channel, out var found) ? found : null;

    public async Task<IReadOnlyList<NotificationChannel>> ConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var configured = new List<NotificationChannel>();

        foreach (var channel in _byChannel.Values)
        {
            if (await channel.IsConfiguredAsync(cancellationToken))
            {
                configured.Add(channel.Channel);
            }
        }

        return configured;
    }
}

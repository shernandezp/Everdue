using Common.Mediator;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Channels;

public sealed record ListChannelSettingsQuery : IQuery<IReadOnlyList<ChannelSettingsDto>>;

public sealed record ChannelHealthQuery : IQuery<IReadOnlyList<ChannelHealthDto>>;

/// <summary>
/// The config arrives as the channel's own JSON shape — the server never invents a second schema for
/// what a channel already defines. A blank secret means "keep the stored one", so an admin can flip
/// Active without re-typing a token they cannot read back.
/// </summary>
public sealed record SaveChannelSettingsCommand(
    NotificationChannel Channel,
    string ConfigJson,
    bool Active) : ICommand<ChannelSettingsDto>;

public sealed record DeleteChannelSettingsCommand(NotificationChannel Channel) : ICommand<bool>;

public sealed record TestChannelCommand(NotificationChannel Channel) : ICommand<ChannelTestResultDto>;

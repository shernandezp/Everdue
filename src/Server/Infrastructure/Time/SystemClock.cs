using Everdue.Server.Application.Abstractions;

namespace Everdue.Server.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

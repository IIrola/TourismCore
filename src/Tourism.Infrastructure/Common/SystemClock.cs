using Tourism.Application.Common.Ports;

namespace Tourism.Infrastructure.Common;

/// <summary>The real-time <see cref="IClock"/>. Swapped for a fixed clock in tests.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

using System;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// A clock that moves only when a test moves it, so "less than a minute ago" can be asserted on both sides
/// of the minute.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks = TimeSpan.TicksPerDay;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => _ticks;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    internal void Advance(TimeSpan by) => _ticks += by.Ticks;
}

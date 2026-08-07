using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was told.
/// </summary>
/// <typeparam name="T">The category the logger belongs to.</typeparam>
/// <remarks>
/// Used where a log line is part of the contract rather than a diagnostic aside: the settings store answers
/// a file it cannot use with the defaults <i>and</i> a warning, and an answer with no warning would be a
/// silent loss of the user's preference.
/// </remarks>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    /// <summary>Gets every entry logged, in order.</summary>
    internal IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _entries.Add((logLevel, formatter(state, exception)));
    }
}

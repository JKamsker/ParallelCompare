using System;

namespace FsEqual.App.Interactive;

/// <summary>
/// Represents the log verbosity levels available in the interactive UI.
/// </summary>
public enum InteractiveVerbosity
{
    Trace,
    Debug,
    Info,
    Warn,
    Error
}

/// <summary>
/// Helper methods for working with <see cref="InteractiveVerbosity"/> values.
/// </summary>
public static class InteractiveVerbosityExtensions
{
    /// <summary>
    /// Parses a string into an <see cref="InteractiveVerbosity"/> value.
    /// </summary>
    /// <param name="value">Verbosity identifier (e.g. <c>debug</c>).</param>
    /// <returns>The resolved verbosity.</returns>
    public static InteractiveVerbosity Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return InteractiveVerbosity.Info;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "trace" => InteractiveVerbosity.Trace,
            "debug" => InteractiveVerbosity.Debug,
            "warn" or "warning" => InteractiveVerbosity.Warn,
            "error" => InteractiveVerbosity.Error,
            _ => InteractiveVerbosity.Info
        };
    }

    /// <summary>
    /// Cycles to the next verbosity level.
    /// </summary>
    /// <param name="verbosity">Current verbosity.</param>
    /// <returns>The next verbosity in the cycle.</returns>
    public static InteractiveVerbosity Next(this InteractiveVerbosity verbosity)
        => verbosity switch
        {
            InteractiveVerbosity.Trace => InteractiveVerbosity.Debug,
            InteractiveVerbosity.Debug => InteractiveVerbosity.Info,
            InteractiveVerbosity.Info => InteractiveVerbosity.Warn,
            InteractiveVerbosity.Warn => InteractiveVerbosity.Error,
            InteractiveVerbosity.Error => InteractiveVerbosity.Trace,
            _ => InteractiveVerbosity.Info
        };

    /// <summary>
    /// Converts the verbosity to a user-friendly display name.
    /// </summary>
    /// <param name="verbosity">Verbosity value.</param>
    /// <returns>Human-readable display name.</returns>
    public static string ToDisplayName(this InteractiveVerbosity verbosity)
        => verbosity switch
        {
            InteractiveVerbosity.Trace => "Trace",
            InteractiveVerbosity.Debug => "Debug",
            InteractiveVerbosity.Info => "Info",
            InteractiveVerbosity.Warn => "Warn",
            InteractiveVerbosity.Error => "Error",
            _ => verbosity.ToString()
        };
}

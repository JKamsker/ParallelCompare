using System;

namespace ParallelCompare.App.Interactive;

public enum InteractiveVerbosity
{
    Trace,
    Debug,
    Info,
    Warn,
    Error
}

public static class InteractiveVerbosityExtensions
{
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

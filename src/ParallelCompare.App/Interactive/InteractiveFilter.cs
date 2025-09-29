using System;

namespace ParallelCompare.App.Interactive;

public enum InteractiveFilter
{
    All,
    Differences,
    LeftOnly,
    RightOnly,
    Errors
}

public static class InteractiveFilterExtensions
{
    public static InteractiveFilter Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return InteractiveFilter.All;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "diff" or "difference" or "differences" => InteractiveFilter.Differences,
            "left" or "leftonly" or "left-only" => InteractiveFilter.LeftOnly,
            "right" or "rightonly" or "right-only" => InteractiveFilter.RightOnly,
            "error" or "errors" => InteractiveFilter.Errors,
            _ => InteractiveFilter.All
        };
    }

    public static string ToDisplayName(this InteractiveFilter filter)
        => filter switch
        {
            InteractiveFilter.All => "All",
            InteractiveFilter.Differences => "Differences",
            InteractiveFilter.LeftOnly => "Left Only",
            InteractiveFilter.RightOnly => "Right Only",
            InteractiveFilter.Errors => "Errors",
            _ => filter.ToString()
        };
}

using System;

namespace FsEqual.App.Interactive;

/// <summary>
/// Defines the subset of nodes displayed in the interactive tree.
/// </summary>
public enum InteractiveFilter
{
    All,
    Differences,
    LeftOnly,
    RightOnly,
    Errors
}

/// <summary>
/// Helper methods for working with <see cref="InteractiveFilter"/> values.
/// </summary>
public static class InteractiveFilterExtensions
{
    /// <summary>
    /// Parses a string into an <see cref="InteractiveFilter"/> value.
    /// </summary>
    /// <param name="value">Filter identifier (e.g. <c>diff</c>, <c>errors</c>).</param>
    /// <returns>The resolved filter value.</returns>
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

    /// <summary>
    /// Converts the filter to a user-friendly display name.
    /// </summary>
    /// <param name="filter">Filter value.</param>
    /// <returns>Human-readable display name.</returns>
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

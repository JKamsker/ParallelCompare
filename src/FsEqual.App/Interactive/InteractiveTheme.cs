using System;

namespace FsEqual.App.Interactive;

/// <summary>
/// Represents the color palette used by the interactive interface.
/// </summary>
public sealed class InteractiveTheme
{
    /// <summary>
    /// Gets the accent color used for headings and highlights.
    /// </summary>
    public string Accent { get; }

    /// <summary>
    /// Gets the muted color used for secondary information.
    /// </summary>
    public string Muted { get; }

    /// <summary>
    /// Gets the color representing equal results.
    /// </summary>
    public string Equal { get; }

    /// <summary>
    /// Gets the color representing differences.
    /// </summary>
    public string Different { get; }

    /// <summary>
    /// Gets the color representing entries only present on the left.
    /// </summary>
    public string LeftOnly { get; }

    /// <summary>
    /// Gets the color representing entries only present on the right.
    /// </summary>
    public string RightOnly { get; }

    /// <summary>
    /// Gets the color representing error states.
    /// </summary>
    public string Error { get; }

    private InteractiveTheme(
        string accent,
        string muted,
        string equal,
        string different,
        string leftOnly,
        string rightOnly,
        string error)
    {
        Accent = accent;
        Muted = muted;
        Equal = equal;
        Different = different;
        LeftOnly = leftOnly;
        RightOnly = rightOnly;
        Error = error;
    }

    /// <summary>
    /// Gets the default dark theme used by the application.
    /// </summary>
    public static InteractiveTheme Dark { get; } = new(
        accent: "deepskyblue1",
        muted: "grey58",
        equal: "green3",
        different: "yellow3",
        leftOnly: "lightskyblue1",
        rightOnly: "mediumorchid1",
        error: "red1");

    /// <summary>
    /// Gets the light theme alternative.
    /// </summary>
    public static InteractiveTheme Light { get; } = new(
        accent: "darkcyan",
        muted: "grey42",
        equal: "darkgreen",
        different: "darkorange3",
        leftOnly: "royalblue1",
        rightOnly: "darkmagenta",
        error: "red3");

    /// <summary>
    /// Parses a theme identifier into a predefined theme.
    /// </summary>
    /// <param name="value">Theme identifier such as <c>dark</c> or <c>light</c>.</param>
    /// <returns>The resolved theme.</returns>
    public static InteractiveTheme Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Dark;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "light" => Light,
            "dark" => Dark,
            _ => Dark
        };
    }
}

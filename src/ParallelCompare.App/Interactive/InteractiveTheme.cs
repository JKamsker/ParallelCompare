using System;

namespace ParallelCompare.App.Interactive;

public sealed class InteractiveTheme
{
    public string Accent { get; }
    public string Muted { get; }
    public string Equal { get; }
    public string Different { get; }
    public string LeftOnly { get; }
    public string RightOnly { get; }
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

    public static InteractiveTheme Dark { get; } = new(
        accent: "deepskyblue1",
        muted: "grey58",
        equal: "green3",
        different: "yellow3",
        leftOnly: "lightskyblue1",
        rightOnly: "mediumorchid1",
        error: "red1");

    public static InteractiveTheme Light { get; } = new(
        accent: "darkcyan",
        muted: "grey42",
        equal: "darkgreen",
        different: "darkorange3",
        leftOnly: "royalblue1",
        rightOnly: "darkmagenta",
        error: "red3");

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

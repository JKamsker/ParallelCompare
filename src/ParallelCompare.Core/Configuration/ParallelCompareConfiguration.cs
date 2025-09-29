using System.Text.Json.Serialization;

namespace ParallelCompare.Core.Configuration;

public sealed record ParallelCompareConfiguration
{
    [JsonPropertyName("defaults")]
    public CompareProfile Defaults { get; init; } = new();

    [JsonPropertyName("profiles")]
    public Dictionary<string, CompareProfile> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public class CompareProfile
{
    [JsonPropertyName("left")]
    public string? Left { get; init; }

    [JsonPropertyName("right")]
    public string? Right { get; init; }

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("algorithms")]
    public string[]? Algorithms { get; init; }

    [JsonPropertyName("ignore")]
    public string[]? Ignore { get; init; }

    [JsonPropertyName("caseSensitive")]
    public bool? CaseSensitive { get; init; }

    [JsonPropertyName("followSymlinks")]
    public bool? FollowSymlinks { get; init; }

    [JsonPropertyName("mtimeToleranceSeconds")]
    public double? MTimeToleranceSeconds { get; init; }

    [JsonPropertyName("threads")]
    public int? Threads { get; init; }

    [JsonPropertyName("baseline")]
    public string? Baseline { get; init; }

    [JsonPropertyName("json")]
    public string? JsonReport { get; init; }

    [JsonPropertyName("summary")]
    public string? SummaryReport { get; init; }

    [JsonPropertyName("export")]
    public string? ExportFormat { get; init; }

    [JsonPropertyName("noProgress")]
    public bool? NoProgress { get; init; }

    [JsonPropertyName("diffTool")]
    public string? DiffTool { get; init; }

    [JsonPropertyName("verbosity")]
    public string? Verbosity { get; init; }

    [JsonPropertyName("interactiveTheme")]
    public string? InteractiveTheme { get; init; }

    [JsonPropertyName("interactiveFilter")]
    public string? InteractiveFilter { get; init; }

    [JsonPropertyName("interactiveVerbosity")]
    public string? InteractiveVerbosity { get; init; }
}

using System.Text.Json;
using FsEqual.Tool.Core;

namespace FsEqual.Tool.Configuration;

internal sealed class ConfigLoader
{
    private const string DefaultFileName = "fsequal.config.json";

    public async Task<ToolConfig?> LoadAsync(string? explicitPath, CancellationToken token)
    {
        var path = ResolvePath(explicitPath);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        var config = await JsonSerializer.DeserializeAsync<ToolConfig>(stream, options, token);
        if (config is null)
        {
            return null;
        }

        if (config.Profiles is not null)
        {
            config.Profiles = new Dictionary<string, CompareProfile>(config.Profiles, StringComparer.OrdinalIgnoreCase);
        }

        config.SourcePath = path;
        return config;
    }

    private static string? ResolvePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var defaultCandidate = Path.Combine(Environment.CurrentDirectory, DefaultFileName);
        return File.Exists(defaultCandidate) ? defaultCandidate : null;
    }
}

internal sealed class ToolConfig
{
    public CompareProfile? Defaults { get; set; }

    public Dictionary<string, CompareProfile>? Profiles { get; set; }

    public string? SourcePath { get; set; }
}

internal sealed class CompareProfile
{
    public string? Mode { get; set; }

    public string? Algorithm { get; set; }

    public int? Threads { get; set; }

    public double? MtimeTolerance { get; set; }

    public string? Verbosity { get; set; }

    public string? FailOn { get; set; }

    public int? TimeoutSeconds { get; set; }

    public List<string>? Ignore { get; set; }

    public bool? CaseSensitive { get; set; }

    public bool? FollowSymlinks { get; set; }

    public bool? NoProgress { get; set; }

    public bool? Interactive { get; set; }
}

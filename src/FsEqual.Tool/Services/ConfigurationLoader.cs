using System.Text.Json;
using System.Text.Json.Serialization;

namespace FsEqual.Tool.Services;

public sealed class ConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<ProfileSettings?> LoadAsync(string? configPath, string? profileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return null;
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file '{configPath}' not found.");
        }

        await using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = await JsonSerializer.DeserializeAsync<ConfigDocument>(stream, Options, cancellationToken)
            ?? throw new InvalidOperationException($"Invalid configuration file '{configPath}'.");

        var name = profileName ?? document.DefaultProfile ?? "default";
        if (!document.Profiles.TryGetValue(name, out var profile))
        {
            throw new InvalidOperationException($"Profile '{name}' was not found in '{configPath}'.");
        }

        return profile;
    }

    private sealed class ConfigDocument
    {
        public string? DefaultProfile { get; set; }
        public Dictionary<string, ProfileSettings> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
    public sealed class ProfileSettings
    {
        public List<string> Ignore { get; set; } = new();
        public int? Threads { get; set; }
        public string? Algo { get; set; }
        public string? Mode { get; set; }
        public bool? CaseSensitive { get; set; }
        public bool? FollowSymlinks { get; set; }
        public double? MtimeTolerance { get; set; }
        public bool? NoProgress { get; set; }
        public string? Json { get; set; }
        public string? Summary { get; set; }
    }
}

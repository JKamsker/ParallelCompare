using System.Text.Json;

namespace ParallelCompare.Core.Configuration;

public sealed class ConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ParallelCompareConfiguration?> LoadAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}", path);
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<ParallelCompareConfiguration>(stream, Options, cancellationToken)
            ?? new ParallelCompareConfiguration();

        return config;
    }
}

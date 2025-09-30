using System.Text.Json;

namespace ParallelCompare.Core.Configuration;

/// <summary>
/// Loads <see cref="ParallelCompareConfiguration"/> instances from disk.
/// </summary>
public sealed class ConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads a configuration file from the specified path.
    /// </summary>
    /// <param name="path">Path to the JSON configuration file.</param>
    /// <param name="cancellationToken">Token used to cancel the load operation.</param>
    /// <returns>The parsed configuration, or <c>null</c> when the path is empty.</returns>
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

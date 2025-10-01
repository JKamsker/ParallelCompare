using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FsEqual.Core.Configuration;

/// <summary>
/// Loads <see cref="FsEqualConfiguration"/> instances from disk.
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
    public async Task<FsEqualConfiguration?> LoadAsync(string? path, CancellationToken cancellationToken)
    {
        var resolvedPath = ResolvePath(path);
        if (resolvedPath is null)
        {
            return null;
        }

        await using var stream = File.OpenRead(resolvedPath);

        FsEqualConfiguration config;
        try
        {
            config = await JsonSerializer.DeserializeAsync<FsEqualConfiguration>(stream, Options, cancellationToken)
                ?? new FsEqualConfiguration();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Configuration file '{resolvedPath}' could not be parsed: {ex.Message}", ex);
        }

        ValidateConfiguration(config, resolvedPath);
        return config;
    }

    private static string? ResolvePath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var absolute = Path.GetFullPath(path);
            if (!File.Exists(absolute))
            {
                throw new FileNotFoundException($"Configuration file not found: {absolute}", absolute);
            }

            return absolute;
        }

        return FindDefaultConfiguration();
    }

    private static string? FindDefaultConfiguration()
    {
        var candidates = EnumerateCandidates();
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = Directory.GetCurrentDirectory();

        foreach (var ancestor in EnumerateAncestors(current))
        {
            foreach (var relative in RelativeSearchPaths)
            {
                var candidate = Path.GetFullPath(Path.Combine(ancestor, relative));
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        foreach (var fallback in EnumerateFallbacks())
        {
            var candidate = Path.GetFullPath(fallback);
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static IEnumerable<string> EnumerateFallbacks()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            foreach (var relative in HomeRelativeSearchPaths)
            {
                yield return Path.Combine(home, relative);
            }
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            yield return Path.Combine(xdgConfigHome, "fsequal", "fsequal.config.json");
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "fsequal", "fsequal.config.json");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "fsequal", "fsequal.config.json");
        }
    }

    private static void ValidateConfiguration(FsEqualConfiguration configuration, string sourcePath)
    {
        var errors = new List<string>();

        if (configuration.Defaults is null)
        {
            errors.Add("defaults section must be an object.");
        }
        else
        {
            CollectValidationErrors(configuration.Defaults, "defaults", errors);
        }

        if (configuration.Profiles is null)
        {
            errors.Add("profiles section must be an object.");
        }
        else
        {
            foreach (var pair in configuration.Profiles)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    errors.Add("profiles cannot contain empty names.");
                    continue;
                }

                if (pair.Value is null)
                {
                    errors.Add($"profile '{pair.Key}' must define an object.");
                    continue;
                }

                CollectValidationErrors(pair.Value, $"profile '{pair.Key}'", errors);
            }
        }

        if (errors.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Configuration file '{sourcePath}' is invalid:");
        foreach (var error in errors)
        {
            builder.Append(" - ");
            builder.AppendLine(error);
        }

        throw new InvalidDataException(builder.ToString().TrimEnd());
    }

    private static void CollectValidationErrors(object instance, string scope, List<string> errors)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true))
        {
            return;
        }

        foreach (var result in results)
        {
            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Validation failed."
                : result.ErrorMessage!;
            errors.Add($"{scope}: {message}");
        }
    }

    private static readonly string[] RelativeSearchPaths =
    {
        "fsequal.config.json",
        Path.Combine(".fsequal", "config.json"),
        Path.Combine(".fsequal", "fsequal.config.json")
    };

    private static readonly string[] HomeRelativeSearchPaths =
    {
        Path.Combine(".config", "fsequal", "fsequal.config.json"),
        Path.Combine(".config", "fsequal", "config.json"),
        Path.Combine(".fsequal", "fsequal.config.json"),
        Path.Combine(".fsequal", "config.json"),
        "fsequal.config.json"
    };
}

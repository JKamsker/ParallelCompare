using System;
using System.Collections.Immutable;
using System.IO;
using FsEqual.Core.Configuration;

namespace FsEqual.Core.Options;

/// <summary>
/// Normalizes compare command inputs by merging configuration defaults and validating options.
/// </summary>
public sealed class CompareSettingsResolver
{
    /// <summary>
    /// Resolves the final comparison settings for the provided input and configuration.
    /// </summary>
    /// <param name="input">Command-line values to resolve.</param>
    /// <param name="configuration">Optional configuration file content.</param>
    /// <returns>The resolved comparison settings.</returns>
    public ResolvedCompareSettings Resolve(
        CompareSettingsInput input,
        FsEqualConfiguration? configuration)
    {
        var defaults = configuration?.Defaults ?? new CompareProfile();
        CompareProfile? profile = null;

        if (!string.IsNullOrWhiteSpace(input.Profile))
        {
            if (configuration is null)
            {
                throw new InvalidOperationException("A configuration file must be provided when using --profile.");
            }

            if (!configuration.Profiles.TryGetValue(input.Profile!, out profile))
            {
                throw new InvalidOperationException($"Profile '{input.Profile}' was not found in the configuration file.");
            }
        }

        var left = input.LeftPath ?? profile?.Left ?? defaults.Left
            ?? throw new InvalidOperationException("Left path must be specified either via CLI or configuration.");
        var right = input.RightPath ?? profile?.Right ?? defaults.Right;
        var baseline = input.BaselinePath ?? profile?.Baseline ?? defaults.Baseline;

        if (string.IsNullOrWhiteSpace(right) && string.IsNullOrWhiteSpace(baseline))
        {
            throw new InvalidOperationException("Right path must be specified either via CLI or configuration unless a baseline manifest is provided.");
        }

        var mode = ParseMode(input.Mode ?? profile?.Mode ?? defaults.Mode);
        var algorithms = ResolveAlgorithms(input, profile, defaults, mode);
        var ignore = MergeArrays(defaults.Ignore, profile?.Ignore, input.IgnorePatterns);

        return new ResolvedCompareSettings
        {
            LeftPath = Path.GetFullPath(left),
            RightPath = string.IsNullOrWhiteSpace(right) ? null : Path.GetFullPath(right),
            Mode = mode,
            Algorithms = algorithms,
            IgnorePatterns = ignore,
            CaseSensitive = input.CaseSensitive ?? profile?.CaseSensitive ?? defaults.CaseSensitive ?? false,
            FollowSymlinks = input.FollowSymlinks ?? profile?.FollowSymlinks ?? defaults.FollowSymlinks ?? false,
            ModifiedTimeTolerance = input.ModifiedTimeTolerance
                ?? ToTimeSpan(profile?.MTimeToleranceSeconds)
                ?? ToTimeSpan(defaults.MTimeToleranceSeconds),
            Threads = input.Threads ?? profile?.Threads ?? defaults.Threads,
            BaselinePath = string.IsNullOrWhiteSpace(baseline) ? null : Path.GetFullPath(baseline),
            JsonReportPath = input.JsonReportPath ?? profile?.JsonReport ?? defaults.JsonReport,
            SummaryReportPath = input.SummaryReportPath ?? profile?.SummaryReport ?? defaults.SummaryReport,
            CsvReportPath = input.CsvReportPath ?? profile?.CsvReport ?? defaults.CsvReport,
            MarkdownReportPath = input.MarkdownReportPath ?? profile?.MarkdownReport ?? defaults.MarkdownReport,
            HtmlReportPath = input.HtmlReportPath ?? profile?.HtmlReport ?? defaults.HtmlReport,
            ExportFormat = input.ExportFormat ?? profile?.ExportFormat ?? defaults.ExportFormat,
            NoProgress = input.NoProgress || profile?.NoProgress == true || defaults.NoProgress == true,
            DiffTool = input.DiffTool ?? profile?.DiffTool ?? defaults.DiffTool,
            Verbosity = input.Verbosity ?? profile?.Verbosity ?? defaults.Verbosity,
            FailOn = input.FailOn,
            Timeout = input.Timeout,
            InteractiveTheme = input.InteractiveTheme ?? profile?.InteractiveTheme ?? defaults.InteractiveTheme,
            InteractiveFilter = input.InteractiveFilter ?? profile?.InteractiveFilter ?? defaults.InteractiveFilter,
            InteractiveVerbosity = input.InteractiveVerbosity
                ?? profile?.InteractiveVerbosity
                ?? defaults.InteractiveVerbosity
                ?? input.Verbosity
                ?? profile?.Verbosity
                ?? defaults.Verbosity,
            UsesBaseline = false,
            BaselineMetadata = null
        };
    }

    private static ComparisonMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ComparisonMode.Quick;
        }

        return value.ToLowerInvariant() switch
        {
            "quick" => ComparisonMode.Quick,
            "hash" => ComparisonMode.Hash,
            _ => throw new InvalidOperationException($"Unsupported comparison mode '{value}'.")
        };
    }

    private static ImmutableArray<HashAlgorithmType> ResolveAlgorithms(
        CompareSettingsInput input,
        CompareProfile? profile,
        CompareProfile defaults,
        ComparisonMode mode)
    {
        var builder = ImmutableArray.CreateBuilder<HashAlgorithmType>();
        var algorithmNames = new List<string>();

        if (!string.IsNullOrWhiteSpace(input.Algorithm))
        {
            algorithmNames.Add(input.Algorithm!);
        }

        if (!input.AdditionalAlgorithms.IsDefaultOrEmpty)
        {
            algorithmNames.AddRange(input.AdditionalAlgorithms);
        }

        if (algorithmNames.Count == 0)
        {
            if (profile?.Algorithms?.Length > 0)
            {
                algorithmNames.AddRange(profile.Algorithms);
            }
            else if (defaults.Algorithms?.Length > 0)
            {
                algorithmNames.AddRange(defaults.Algorithms);
            }
        }

        if (algorithmNames.Count == 0)
        {
            // Default algorithm per spec: crc32 in quick mode, sha256 in hash mode.
            algorithmNames.Add(mode == ComparisonMode.Quick ? "crc32" : "sha256");
        }

        foreach (var name in algorithmNames)
        {
            builder.Add(ParseAlgorithm(name));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> MergeArrays(
        string[]? defaults,
        string[]? profile,
        ImmutableArray<string> overrides)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (defaults is { Length: > 0 })
        {
            foreach (var value in defaults)
            {
                set.Add(value);
            }
        }

        if (profile is { Length: > 0 })
        {
            foreach (var value in profile)
            {
                set.Add(value);
            }
        }

        if (!overrides.IsDefaultOrEmpty)
        {
            foreach (var value in overrides)
            {
                set.Add(value);
            }
        }

        return set.ToImmutableArray();
    }

    private static TimeSpan? ToTimeSpan(double? seconds)
        => seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);

    private static HashAlgorithmType ParseAlgorithm(string value)
        => value.ToLowerInvariant() switch
        {
            "crc32" => HashAlgorithmType.Crc32,
            "md5" => HashAlgorithmType.Md5,
            "sha256" => HashAlgorithmType.Sha256,
            "xxh64" or "xxhash64" or "xxhash" => HashAlgorithmType.XxHash64,
            _ => throw new InvalidOperationException($"Unsupported hash algorithm '{value}'.")
        };
}

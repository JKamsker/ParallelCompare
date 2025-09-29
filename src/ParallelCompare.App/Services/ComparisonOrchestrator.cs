using System;
using System.Collections.Immutable;
using System.Linq;
using ParallelCompare.App.Commands;
using ParallelCompare.Core.Comparison;
using ParallelCompare.Core.Configuration;
using ParallelCompare.Core.Options;
using ParallelCompare.Core.Reporting;

namespace ParallelCompare.App.Services;

public sealed class ComparisonOrchestrator
{
    private readonly ConfigurationLoader _configurationLoader = new();
    private readonly CompareSettingsResolver _resolver = new();
    private readonly ComparisonEngine _engine = new();
    private readonly ComparisonResultExporter _exporter = new();

    public CompareSettingsInput BuildInput(CompareCommandSettings settings)
    {
        var algorithms = settings.Algorithms ?? Array.Empty<string>();
        var algorithm = algorithms.FirstOrDefault();
        var additional = algorithms.Length > 1
            ? algorithms.Skip(1).ToImmutableArray()
            : ImmutableArray<string>.Empty;

        return new CompareSettingsInput
        {
            LeftPath = settings.Left,
            RightPath = settings.Right,
            Mode = settings.Mode,
            Algorithm = algorithm,
            AdditionalAlgorithms = additional,
            IgnorePatterns = settings.Ignore.Length > 0 ? settings.Ignore.ToImmutableArray() : ImmutableArray<string>.Empty,
            CaseSensitive = settings.CaseSensitive ? true : null,
            FollowSymlinks = settings.FollowSymlinks ? true : null,
            ModifiedTimeTolerance = settings.ModifiedTimeToleranceSeconds.HasValue
                ? TimeSpan.FromSeconds(settings.ModifiedTimeToleranceSeconds.Value)
                : null,
            Threads = settings.Threads,
            BaselinePath = settings.Baseline,
            JsonReportPath = settings.JsonReport,
            SummaryReportPath = settings.SummaryReport,
            ExportFormat = settings.ExportFormat,
            NoProgress = settings.NoProgress,
            DiffTool = settings.DiffTool,
            Profile = settings.Profile,
            ConfigurationPath = settings.ConfigurationPath,
            Verbosity = settings.Verbosity,
            FailOn = settings.FailOn,
            Timeout = settings.TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(settings.TimeoutSeconds.Value) : null,
            EnableInteractive = settings.Interactive
        };
    }

    public async Task<(ComparisonResult Result, ResolvedCompareSettings Resolved)> RunAsync(
        CompareSettingsInput input,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationLoader.LoadAsync(input.ConfigurationPath, cancellationToken);
        var resolved = _resolver.Resolve(input, configuration);

        var options = new ComparisonOptions
        {
            LeftPath = resolved.LeftPath,
            RightPath = resolved.RightPath,
            Mode = resolved.Mode,
            HashAlgorithms = resolved.Algorithms,
            IgnorePatterns = resolved.IgnorePatterns,
            CaseSensitive = resolved.CaseSensitive,
            FollowSymlinks = resolved.FollowSymlinks,
            ModifiedTimeTolerance = resolved.ModifiedTimeTolerance,
            MaxDegreeOfParallelism = resolved.Threads,
            BaselinePath = resolved.BaselinePath,
            EnableInteractive = input.EnableInteractive,
            JsonReportPath = resolved.JsonReportPath,
            SummaryReportPath = resolved.SummaryReportPath,
            ExportFormat = resolved.ExportFormat,
            NoProgress = resolved.NoProgress,
            DiffTool = resolved.DiffTool,
            CancellationToken = cancellationToken
        };

        var result = await _engine.CompareAsync(options);

        if (!string.IsNullOrWhiteSpace(resolved.JsonReportPath))
        {
            await _exporter.WriteJsonAsync(result, resolved.JsonReportPath!, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(resolved.SummaryReportPath))
        {
            await _exporter.WriteSummaryAsync(result, resolved.SummaryReportPath!, cancellationToken);
        }

        return (result, resolved);
    }
}

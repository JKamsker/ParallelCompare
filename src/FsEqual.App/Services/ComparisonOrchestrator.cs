using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using FsEqual.App.Commands;
using FsEqual.Core.Baselines;
using FsEqual.Core.Comparison;
using FsEqual.Core.Configuration;
using FsEqual.Core.Options;
using FsEqual.Core.Reporting;

namespace FsEqual.App.Services;

/// <summary>
/// Coordinates configuration resolution, comparison execution, and exporting.
/// </summary>
public sealed class ComparisonOrchestrator
{
    private readonly ConfigurationLoader _configurationLoader = new();
    private readonly CompareSettingsResolver _resolver = new();
    private readonly ComparisonEngine _engine = new();
    private readonly ExportRegistry _exportRegistry = new();
    private readonly BaselineComparisonEngine _baselineEngine = new();
    private readonly BaselineSnapshotGenerator _snapshotGenerator = new();
    private readonly BaselineManifestSerializer _baselineSerializer = new();

    /// <summary>
    /// Builds a <see cref="CompareSettingsInput"/> from raw command settings.
    /// </summary>
    /// <param name="settings">Settings supplied by the command.</param>
    /// <returns>The normalized input record.</returns>
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
            CsvReportPath = settings.CsvReport,
            MarkdownReportPath = settings.MarkdownReport,
            HtmlReportPath = settings.HtmlReport,
            ExportFormat = settings.ExportFormat,
            NoProgress = settings.NoProgress,
            DiffTool = settings.DiffTool,
            Profile = settings.Profile,
            ConfigurationPath = settings.ConfigurationPath,
            Verbosity = settings.Verbosity,
            FailOn = settings.FailOn,
            Timeout = settings.TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(settings.TimeoutSeconds.Value) : null,
            EnableInteractive = settings.Interactive,
            SummaryFilter = settings.SummaryFilter
        };
    }

    /// <summary>
    /// Resolves configuration, executes the comparison or baseline run, and writes exports.
    /// </summary>
    /// <param name="input">Comparison input describing the requested run.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The comparison result and resolved settings.</returns>
    public async Task<(ComparisonResult Result, ResolvedCompareSettings Resolved)> RunAsync(
        CompareSettingsInput input,
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink = null)
    {
        var resolved = await ResolveAsync(input, cancellationToken);

        ComparisonResult result;
        if (ShouldUseBaseline(resolved))
        {
            (result, resolved) = await RunBaselineComparisonAsync(resolved, cancellationToken, progressSink);
        }
        else
        {
            result = await RunStandardComparisonAsync(resolved, input.EnableInteractive, cancellationToken, progressSink);
        }

        await WriteExportsAsync(result, resolved, cancellationToken);
        return (result, resolved);
    }

    /// <summary>
    /// Resolves the supplied input into concrete comparison settings without executing a run.
    /// </summary>
    /// <param name="input">Command-line input to resolve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The resolved comparison settings.</returns>
    public async Task<ResolvedCompareSettings> ResolveAsync(
        CompareSettingsInput input,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationLoader.LoadAsync(input.ConfigurationPath, cancellationToken);
        var resolved = _resolver.Resolve(input, configuration);
        return resolved with { UsesBaseline = false, BaselineMetadata = null };
    }

    /// <summary>
    /// Creates a baseline snapshot and writes it to the specified path.
    /// </summary>
    /// <param name="input">Comparison input describing what to snapshot.</param>
    /// <param name="outputPath">Destination path for the manifest.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The generated baseline manifest.</returns>
    public async Task<BaselineManifest> CreateSnapshotAsync(
        CompareSettingsInput input,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(input, cancellationToken);
        var manifest = _snapshotGenerator.CreateSnapshot(resolved, cancellationToken);
        var destination = Path.GetFullPath(outputPath);
        await _baselineSerializer.WriteAsync(manifest, destination, cancellationToken);
        return manifest;
    }

    private async Task<ComparisonResult> RunStandardComparisonAsync(
        ResolvedCompareSettings resolved,
        bool enableInteractive,
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink)
    {
        if (string.IsNullOrWhiteSpace(resolved.RightPath))
        {
            throw new InvalidOperationException("Right path must be provided when not using a baseline manifest.");
        }

        var options = new ComparisonOptions
        {
            LeftPath = resolved.LeftPath,
            RightPath = resolved.RightPath!,
            Mode = resolved.Mode,
            HashAlgorithms = resolved.Algorithms,
            IgnorePatterns = resolved.IgnorePatterns,
            CaseSensitive = resolved.CaseSensitive,
            FollowSymlinks = resolved.FollowSymlinks,
            ModifiedTimeTolerance = resolved.ModifiedTimeTolerance,
            MaxDegreeOfParallelism = resolved.Threads,
            BaselinePath = resolved.BaselinePath,
            EnableInteractive = enableInteractive,
            JsonReportPath = resolved.JsonReportPath,
            SummaryReportPath = resolved.SummaryReportPath,
            CsvReportPath = resolved.CsvReportPath,
            MarkdownReportPath = resolved.MarkdownReportPath,
            HtmlReportPath = resolved.HtmlReportPath,
            ExportFormat = resolved.ExportFormat,
            NoProgress = resolved.NoProgress,
            DiffTool = resolved.DiffTool,
            CancellationToken = cancellationToken,
            FileSystem = resolved.FileSystem,
            ProgressSink = progressSink
        };

        return await _engine.CompareAsync(options);
    }

    private async Task<(ComparisonResult Result, ResolvedCompareSettings Resolved)> RunBaselineComparisonAsync(
        ResolvedCompareSettings resolved,
        CancellationToken cancellationToken,
        IComparisonProgressSink? progressSink)
    {
        if (string.IsNullOrWhiteSpace(resolved.BaselinePath))
        {
            throw new InvalidOperationException("A baseline manifest path must be provided when running in baseline mode.");
        }

        var manifestPath = Path.GetFullPath(resolved.BaselinePath);
        var manifest = await _baselineSerializer.ReadAsync(manifestPath, cancellationToken);
        ValidateBaselineCompatibility(resolved, manifest);

        var algorithms = resolved.Algorithms.IsDefaultOrEmpty && !manifest.Algorithms.IsDefaultOrEmpty
            ? manifest.Algorithms
            : resolved.Algorithms;

        var options = new BaselineComparisonOptions
        {
            LeftPath = resolved.LeftPath,
            BaselinePath = manifestPath,
            Manifest = manifest,
            HashAlgorithms = algorithms,
            IgnorePatterns = resolved.IgnorePatterns,
            CaseSensitive = resolved.CaseSensitive,
            ModifiedTimeTolerance = resolved.ModifiedTimeTolerance,
            Mode = resolved.Mode,
            CancellationToken = cancellationToken,
            FileSystem = resolved.FileSystem,
            ProgressSink = progressSink
        };

        var result = await _baselineEngine.CompareAsync(options);
        var metadata = result.Baseline ?? new BaselineMetadata(
            manifestPath,
            manifest.SourcePath,
            manifest.CreatedAt,
            algorithms);

        var updatedResolved = resolved with
        {
            UsesBaseline = true,
            BaselineMetadata = metadata,
            RightPath = manifest.SourcePath,
            Algorithms = algorithms
        };

        return (result with { Baseline = metadata }, updatedResolved);
    }

    private async Task WriteExportsAsync(
        ComparisonResult result,
        ResolvedCompareSettings resolved,
        CancellationToken cancellationToken)
    {
        var requests = BuildExportRequests(resolved);
        if (requests.Count == 0)
        {
            return;
        }

        await _exportRegistry.ExportAsync(result, resolved, requests, cancellationToken);
    }

    private static List<ReportExportRequest> BuildExportRequests(ResolvedCompareSettings resolved)
    {
        var requests = new List<ReportExportRequest>();
        AddRequest(requests, ReportExportFormats.Json, resolved.JsonReportPath);
        AddRequest(requests, ReportExportFormats.Summary, resolved.SummaryReportPath);
        AddRequest(requests, ReportExportFormats.Csv, resolved.CsvReportPath);
        AddRequest(requests, ReportExportFormats.Markdown, resolved.MarkdownReportPath);
        AddRequest(requests, ReportExportFormats.Html, resolved.HtmlReportPath);
        return requests;
    }

    private static void AddRequest(ICollection<ReportExportRequest> requests, string format, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        requests.Add(new ReportExportRequest(format, path));
    }

    private static bool ShouldUseBaseline(ResolvedCompareSettings resolved)
        => !string.IsNullOrWhiteSpace(resolved.BaselinePath) && string.IsNullOrWhiteSpace(resolved.RightPath);

    private static void ValidateBaselineCompatibility(ResolvedCompareSettings resolved, BaselineManifest manifest)
    {
        if (!SequenceEqual(resolved.IgnorePatterns, manifest.IgnorePatterns, resolved.CaseSensitive))
        {
            throw new InvalidOperationException("Ignore patterns for the current run do not match the baseline manifest.");
        }

        if (resolved.CaseSensitive != manifest.CaseSensitive)
        {
            throw new InvalidOperationException("Case-sensitivity setting does not match the baseline manifest.");
        }

        if (resolved.ModifiedTimeTolerance != manifest.ModifiedTimeTolerance)
        {
            throw new InvalidOperationException("Modified time tolerance does not match the baseline manifest.");
        }

        if (!manifest.Algorithms.IsDefaultOrEmpty && !resolved.Algorithms.IsDefaultOrEmpty)
        {
            var resolvedAlgorithms = resolved.Algorithms.OrderBy(a => a).ToArray();
            var baselineAlgorithms = manifest.Algorithms.OrderBy(a => a).ToArray();
            if (!resolvedAlgorithms.SequenceEqual(baselineAlgorithms))
            {
                throw new InvalidOperationException("Hash algorithms do not match the baseline manifest.");
            }
        }
    }

    private static bool SequenceEqual(
        ImmutableArray<string> first,
        ImmutableArray<string> second,
        bool caseSensitive)
    {
        if (first.IsDefaultOrEmpty && second.IsDefaultOrEmpty)
        {
            return true;
        }

        var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var normalizedFirst = first.IsDefault ? ImmutableArray<string>.Empty : first;
        var normalizedSecond = second.IsDefault ? ImmutableArray<string>.Empty : second;
        return normalizedFirst.OrderBy(x => x, comparer).SequenceEqual(normalizedSecond.OrderBy(x => x, comparer), comparer);
    }
}

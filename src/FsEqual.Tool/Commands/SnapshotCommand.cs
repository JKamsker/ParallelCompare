using System.Collections.Concurrent;
using System.Text.Json;
using System.IO;
using FsEqual.Tool.Comparison;
using FsEqual.Tool.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class SnapshotCommand : AsyncCommand<SnapshotSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SnapshotSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Output) && string.IsNullOrWhiteSpace(settings.Compare))
        {
            throw new CliUsageException("Specify --output to create a snapshot or --compare to validate against an existing snapshot.");
        }

        var options = BuildOptions(settings);
        using var cts = new CancellationTokenSource();

        try
        {
            if (!string.IsNullOrWhiteSpace(settings.Output))
            {
                await CreateSnapshotAsync(options, settings.Output!, cts.Token).ConfigureAwait(false);
                AnsiConsole.MarkupLine($"[green]Snapshot written to[/] {Path.GetFullPath(settings.Output!)}");
            }

            if (!string.IsNullOrWhiteSpace(settings.Compare))
            {
                var compareOptions = options with { BaselinePath = settings.Compare, Right = null };
                var comparer = new DirectoryComparer();
                var result = await comparer.CompareAsync(compareOptions, null, cts.Token).ConfigureAwait(false);
                var compareSettings = new CompareSettings
                {
                    Left = compareOptions.Left,
                    BaselinePath = compareOptions.BaselinePath
                };
                var reporter = new CompareReporter(compareOptions, compareSettings);
                reporter.Render(result);
                return ComparisonExitCodes.Calculate(compareOptions, result);
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Snapshot operation cancelled.[/]");
            return 2;
        }

        return 0;
    }

    private static ComparisonOptions BuildOptions(SnapshotSettings settings)
    {
        return new ComparisonOptions
        {
            Left = settings.Target,
            Right = null,
            BaselinePath = null,
            Mode = ComparisonMode.Hash,
            Algorithm = ParseAlgorithm(settings.Algorithm),
            Ignore = settings.Ignore ?? Array.Empty<string>(),
            CaseSensitive = settings.CaseSensitive,
            FollowSymlinks = settings.FollowSymlinks,
            MaxDegreeOfParallelism = settings.Threads ?? Environment.ProcessorCount,
            MtimeTolerance = null,
            Verbosity = VerbosityLevel.Info,
            Timeout = null,
            FailOn = FailOnRule.Any,
            Profile = null,
            ConfigPath = null
        };
    }

    private static HashAlgorithmKind ParseAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return HashAlgorithmKind.Sha256;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => throw new CliUsageException($"Unknown algorithm '{value}'.")
        };
    }

    private static async Task CreateSnapshotAsync(ComparisonOptions options, string outputPath, CancellationToken cancellationToken)
    {
        var ignore = new IgnoreMatcher(options.Ignore, options.CaseSensitive);
        var snapshot = DirectorySnapshot.Load(options.Left, options, ignore, cancellationToken);
        var files = new ConcurrentBag<SnapshotFile>();

        await Parallel.ForEachAsync(snapshot.Files.Values, new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        }, (file, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var hash = Hashing.ComputeHash(file.FullPath, options.Algorithm, ct);
            files.Add(new SnapshotFile
            {
                Path = file.RelativePath,
                Size = file.Length,
                LastWriteTimeUtc = file.LastWriteTimeUtc,
                Hash = hash,
                HashAlgorithm = options.Algorithm.ToString().ToLowerInvariant()
            });
            return ValueTask.CompletedTask;
        });

        var model = new SnapshotModel
        {
            CaseSensitive = options.CaseSensitive,
            HashAlgorithm = options.Algorithm.ToString().ToLowerInvariant(),
            Files = files.OrderBy(f => f.Path, options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).ToList()
        };

        var json = JsonSerializer.Serialize(model, SnapshotJsonContext.Default.SnapshotModel);
        var path = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }
}

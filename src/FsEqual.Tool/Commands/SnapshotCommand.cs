using FsEqual.Tool.Models;
using FsEqual.Tool.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FsEqual.Tool.Commands;

public sealed class SnapshotCommand : AsyncCommand<SnapshotCommand.Settings>
{
    private readonly SnapshotService _snapshotService = new();

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        public string Path { get; set; } = string.Empty;

        [CommandOption("-o|--output <FILE>")]
        public string Output { get; set; } = "snapshot.json";

        [CommandOption("-a|--algo <ALGO>")]
        public string Algorithm { get; set; } = "sha256";

        [CommandOption("-i|--ignore <GLOB>")]
        public string[] Ignore { get; set; } = Array.Empty<string>();

        [CommandOption("--case-sensitive")]
        public bool CaseSensitive { get; set; }

        [CommandOption("--follow-symlinks")]
        public bool FollowSymlinks { get; set; }

        [CommandOption("-t|--threads <THREADS>")]
        public int Threads { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Path))
        {
            return ValidationResult.Error("Path is required.");
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var cts = new CancellationTokenSource();

        void OnCancel(object? sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            cts.Cancel();
        }

        Console.CancelKeyPress += OnCancel;

        try
        {
            var cancellationToken = cts.Token;
        var algorithm = ParseAlgorithm(settings.Algorithm);
        var options = new ComparisonOptions
        {
            Algorithm = algorithm,
            Mode = ComparisonMode.Hash,
            IgnoreGlobs = settings.Ignore,
            CaseSensitive = settings.CaseSensitive,
            FollowSymlinks = settings.FollowSymlinks,
            Threads = settings.Threads > 0 ? settings.Threads : Environment.ProcessorCount,
        };

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .StartAsync("Creating snapshot...", async _ =>
            {
                var snapshot = await _snapshotService.CaptureAsync(settings.Path, options, algorithm, cancellationToken);
                await _snapshotService.SaveAsync(snapshot, algorithm, settings.Output, settings.CaseSensitive, cancellationToken);
            });

        AnsiConsole.MarkupLine($"[green]Snapshot saved to {settings.Output}[/]");
        return 0;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }

    private static HashAlgorithmKind ParseAlgorithm(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "crc32" => HashAlgorithmKind.Crc32,
            "md5" => HashAlgorithmKind.Md5,
            "sha256" => HashAlgorithmKind.Sha256,
            "xxh64" => HashAlgorithmKind.Xxh64,
            _ => throw new InvalidOperationException($"Unsupported algorithm '{value}'."),
        };
    }
}

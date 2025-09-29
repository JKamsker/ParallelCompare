using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;

namespace FsEqual.Core;

public sealed class ComparisonService
{
    private readonly ComparisonOptions _options;
    private readonly IAnsiConsole _console;

    public ComparisonService(ComparisonOptions options, IAnsiConsole? console = null)
    {
        _options = options;
        _console = console ?? AnsiConsole.Console;
    }

    public async Task<ComparisonResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = _options.TimeoutSeconds is null
            ? null
            : new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds.Value));

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts?.Token ?? CancellationToken.None);

        var token = linkedCts.Token;
        var stopwatch = Stopwatch.StartNew();
        var errors = new ConcurrentBag<string>();

        FileSystemSnapshot leftSnapshot = default!;
        FileSystemSnapshot rightSnapshot = default!;

        async Task EnumerateAsync()
        {
            await Task.Run(() =>
            {
                leftSnapshot = FileSystemSnapshot.Create(_options.LeftRoot, _options.CaseSensitive, _options.FollowSymlinks, _options.IgnoreGlobs, token);
                rightSnapshot = FileSystemSnapshot.Create(_options.RightRoot, _options.CaseSensitive, _options.FollowSymlinks, _options.IgnoreGlobs, token);
            }, token);
        }

        if (_options.NoProgress)
        {
            await EnumerateAsync();
        }
        else
        {
            await _console.Status().StartAsync("Enumerating file system", async _ =>
            {
                await EnumerateAsync();
            });
        }

        ImmutableDictionary<string, string?> leftHashes = ImmutableDictionary<string, string?>.Empty;
        ImmutableDictionary<string, string?> rightHashes = ImmutableDictionary<string, string?>.Empty;

        if (_options.Mode == ComparisonMode.Hash)
        {
            var hashComputer = new HashComputer(_options.HashAlgorithm);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.Threads ?? Environment.ProcessorCount,
                CancellationToken = token
            };

            async Task<ImmutableDictionary<string, string?>> ComputeAsync(string root, FileSystemSnapshot snapshot, string side)
            {
                return await Task.Run(() =>
                {
                    var results = new ConcurrentDictionary<string, string?>(snapshot.Comparer);
                    Parallel.ForEach(snapshot.Entries.Values.Where(e => !e.IsDirectory), parallelOptions, entry =>
                    {
                        var absolutePath = Path.Combine(root, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        try
                        {
                            var hash = hashComputer.ComputeHash(absolutePath, token);
                            results[entry.RelativePath] = hash;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{side}:{entry.RelativePath}: {ex.Message}");
                            results[entry.RelativePath] = null;
                        }
                    });
                    return results.ToImmutableDictionary();
                }, token);
            }

            if (_options.NoProgress)
            {
                leftHashes = await ComputeAsync(_options.LeftRoot, leftSnapshot, "left");
                rightHashes = await ComputeAsync(_options.RightRoot, rightSnapshot, "right");
            }
            else
            {
                await _console.Status().StartAsync("Hashing files", async _ =>
                {
                    leftHashes = await ComputeAsync(_options.LeftRoot, leftSnapshot, "left");
                    rightHashes = await ComputeAsync(_options.RightRoot, rightSnapshot, "right");
                });
            }
        }

        var comparer = _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var allPaths = new SortedSet<string>(comparer);
        foreach (var path in leftSnapshot.Paths)
        {
            allPaths.Add(path);
        }
        foreach (var path in rightSnapshot.Paths)
        {
            allPaths.Add(path);
        }

        var rootBuilder = new NodeBuilder("root", string.Empty, true, null);
        var builders = new Dictionary<string, NodeBuilder>(comparer)
        {
            [string.Empty] = rootBuilder
        };

        NodeBuilder EnsureNode(string relativePath, bool isDirectory)
        {
            if (relativePath.Length == 0)
            {
                return rootBuilder;
            }

            if (!builders.TryGetValue(relativePath, out var existing))
            {
                var parentPath = Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
                var name = Path.GetFileName(relativePath.Replace('/', Path.DirectorySeparatorChar));
                var parent = EnsureNode(parentPath, true);
                existing = new NodeBuilder(name, relativePath, isDirectory, parent);
                parent.Children.Add(existing);
                builders[relativePath] = existing;
            }
            else if (isDirectory && !existing.IsDirectory)
            {
                existing.IsDirectory = true;
            }

            return existing;
        }

        var differences = ImmutableArray.CreateBuilder<DifferenceRecord>();
        var summary = new SummaryBuilder();

        foreach (var path in allPaths)
        {
            var leftExists = leftSnapshot.TryGet(path, out var leftEntry);
            var rightExists = rightSnapshot.TryGet(path, out var rightEntry);
            var isDirectory = leftEntry?.IsDirectory ?? rightEntry?.IsDirectory ?? false;
            var node = EnsureNode(path, isDirectory);
            node.LeftEntry = leftEntry;
            node.RightEntry = rightEntry;

            if (isDirectory)
            {
                summary.TotalDirectories++;
                if (!leftExists)
                {
                    node.Status = DifferenceType.MissingLeft;
                    differences.Add(new DifferenceRecord
                    {
                        Type = DifferenceType.MissingLeft,
                        Path = path,
                        Reason = "Directory missing on left"
                    });
                    summary.MissingLeft++;
                }
                else if (!rightExists)
                {
                    node.Status = DifferenceType.MissingRight;
                    differences.Add(new DifferenceRecord
                    {
                        Type = DifferenceType.MissingRight,
                        Path = path,
                        Reason = "Directory missing on right"
                    });
                    summary.MissingRight++;
                }
                continue;
            }

            summary.TotalFiles++;

            if (!leftExists)
            {
                node.Status = DifferenceType.MissingLeft;
                node.RightMetadata = CreateMetadata(rightEntry!, rightHashes);
                differences.Add(new DifferenceRecord
                {
                    Type = DifferenceType.MissingLeft,
                    Path = path,
                    RightSize = rightEntry!.Size,
                    Reason = "File missing on left"
                });
                summary.MissingLeft++;
                summary.DifferentFiles++;
                continue;
            }

            if (!rightExists)
            {
                node.Status = DifferenceType.MissingRight;
                node.LeftMetadata = CreateMetadata(leftEntry!, leftHashes);
                differences.Add(new DifferenceRecord
                {
                    Type = DifferenceType.MissingRight,
                    Path = path,
                    LeftSize = leftEntry!.Size,
                    Reason = "File missing on right"
                });
                summary.MissingRight++;
                summary.DifferentFiles++;
                continue;
            }

            node.LeftMetadata = CreateMetadata(leftEntry!, leftHashes);
            node.RightMetadata = CreateMetadata(rightEntry!, rightHashes);

            if (leftEntry!.IsDirectory != rightEntry!.IsDirectory)
            {
                node.Status = DifferenceType.TypeMismatch;
                differences.Add(new DifferenceRecord
                {
                    Type = DifferenceType.TypeMismatch,
                    Path = path,
                    Reason = "Item types differ"
                });
                summary.DifferentFiles++;
                continue;
            }

            if (leftEntry.Size != rightEntry.Size)
            {
                node.Status = DifferenceType.SizeMismatch;
                differences.Add(new DifferenceRecord
                {
                    Type = DifferenceType.SizeMismatch,
                    Path = path,
                    LeftSize = leftEntry.Size,
                    RightSize = rightEntry.Size,
                    Reason = "File sizes differ"
                });
                summary.DifferentFiles++;
                continue;
            }

            if (_options.Mode == ComparisonMode.Quick)
            {
                var delta = Math.Abs((leftEntry.LastWriteTimeUtc - rightEntry.LastWriteTimeUtc).TotalSeconds);
                if (delta > _options.MTimeTolerance.TotalSeconds)
                {
                    node.Status = DifferenceType.TimestampMismatch;
                    differences.Add(new DifferenceRecord
                    {
                        Type = DifferenceType.TimestampMismatch,
                        Path = path,
                        Reason = $"Timestamp differs by {delta:F1}s"
                    });
                    summary.DifferentFiles++;
                }
                else
                {
                    summary.EqualFiles++;
                }
                continue;
            }

            if (_options.Mode == ComparisonMode.Hash)
            {
                var leftHash = leftHashes.TryGetValue(path, out var l) ? l : null;
                var rightHash = rightHashes.TryGetValue(path, out var r) ? r : null;
                if (leftHash is null || rightHash is null)
                {
                    node.Status = DifferenceType.Error;
                    differences.Add(new DifferenceRecord
                    {
                        Type = DifferenceType.Error,
                        Path = path,
                        Algorithm = _options.HashAlgorithm.ToString().ToLowerInvariant(),
                        Reason = "Unable to compute hash"
                    });
                    summary.Errors++;
                    continue;
                }

                if (!string.Equals(leftHash, rightHash, StringComparison.OrdinalIgnoreCase))
                {
                    node.Status = DifferenceType.HashMismatch;
                    differences.Add(new DifferenceRecord
                    {
                        Type = DifferenceType.HashMismatch,
                        Path = path,
                        LeftSize = leftEntry.Size,
                        RightSize = rightEntry.Size,
                        Algorithm = _options.HashAlgorithm.ToString().ToLowerInvariant(),
                        Reason = "Hashes differ"
                    });
                    summary.DifferentFiles++;
                }
                else
                {
                    summary.EqualFiles++;
                }
            }
        }

        PopulateNodeStatistics(rootBuilder);

        var summarySnapshot = summary.ToSummary();
        summarySnapshot = summarySnapshot with { Errors = Math.Max(summarySnapshot.Errors, errors.Count) };

        var result = new ComparisonResult
        {
            Summary = summarySnapshot,
            Differences = differences.ToImmutable(),
            RootNode = rootBuilder.ToComparisonNode(),
            Errors = errors.ToImmutableArray(),
            Elapsed = stopwatch.Elapsed
        };

        return result;
    }

    private static FileMetadata CreateMetadata(SnapshotEntry entry, ImmutableDictionary<string, string?> hashes)
    {
        hashes.TryGetValue(entry.RelativePath, out var hash);
        return new FileMetadata
        {
            Size = entry.Size,
            LastWriteTimeUtc = entry.LastWriteTimeUtc,
            Hash = hash
        };
    }

    private static void PopulateNodeStatistics(NodeBuilder node)
    {
        if (node.IsDirectory)
        {
            foreach (var child in node.Children)
            {
                PopulateNodeStatistics(child);
                node.EqualCount += child.EqualCount;
                node.DiffCount += child.DiffCount;
                node.MissingCount += child.MissingCount;
                node.ErrorCount += child.ErrorCount;
            }

            if (node.Status is DifferenceType.MissingLeft or DifferenceType.MissingRight)
            {
                node.MissingCount++;
            }
            else if (node.Status == DifferenceType.Error)
            {
                node.ErrorCount++;
            }
        }
        else
        {
            switch (node.Status)
            {
                case null:
                    node.EqualCount++;
                    break;
                case DifferenceType.Error:
                    node.ErrorCount++;
                    break;
                case DifferenceType.MissingLeft or DifferenceType.MissingRight:
                    node.MissingCount++;
                    break;
                default:
                    node.DiffCount++;
                    break;
            }
        }
    }

    private sealed class NodeBuilder
    {
        public NodeBuilder(string name, string relativePath, bool isDirectory, NodeBuilder? parent)
        {
            Name = name;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
            Parent = parent;
        }

        public string Name { get; }
        public string RelativePath { get; }
        public bool IsDirectory { get; set; }
        public NodeBuilder? Parent { get; }
        public List<NodeBuilder> Children { get; } = new();
        public SnapshotEntry? LeftEntry { get; set; }
        public SnapshotEntry? RightEntry { get; set; }
        public DifferenceType? Status { get; set; }
        public int EqualCount { get; set; }
        public int DiffCount { get; set; }
        public int MissingCount { get; set; }
        public int ErrorCount { get; set; }
        public FileMetadata? LeftMetadata { get; set; }
        public FileMetadata? RightMetadata { get; set; }

        public ComparisonNode ToComparisonNode()
        {
            return new ComparisonNode
            {
                Name = string.IsNullOrEmpty(Name) ? "root" : Name,
                RelativePath = RelativePath,
                IsDirectory = IsDirectory,
                Status = Status,
                Children = Children
                    .OrderBy(c => c.IsDirectory ? 0 : 1)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(c => c.ToComparisonNode())
                    .ToImmutableArray(),
                EqualCount = EqualCount,
                DiffCount = DiffCount,
                MissingCount = MissingCount,
                ErrorCount = ErrorCount,
                LeftMetadata = LeftMetadata,
                RightMetadata = RightMetadata
            };
        }
    }

    private sealed class SummaryBuilder
    {
        public int TotalFiles;
        public int TotalDirectories;
        public int EqualFiles;
        public int DifferentFiles;
        public int MissingLeft;
        public int MissingRight;
        public int Errors;

        public ComparisonSummary ToSummary() => new()
        {
            TotalFiles = TotalFiles,
            TotalDirectories = TotalDirectories,
            EqualFiles = EqualFiles,
            DifferentFiles = DifferentFiles,
            MissingLeft = MissingLeft,
            MissingRight = MissingRight,
            Errors = Errors
        };
    }
}

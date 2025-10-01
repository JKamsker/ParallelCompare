using System;
using System.Diagnostics;
using System.Threading;
using FsEqual.Core.Comparison;

namespace FsEqual.App.Services;

/// <summary>
/// Aggregates comparison progress metrics for console rendering.
/// </summary>
public sealed class ComparisonProgressTracker : IComparisonProgressSink
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _leftBytes;
    private long _rightBytes;
    private int _discoveredFiles;
    private int _completedFiles;

    /// <summary>
    /// Creates a snapshot of the current progress metrics.
    /// </summary>
    /// <returns>The immutable snapshot.</returns>
    public ComparisonProgressSnapshot GetSnapshot()
    {
        var elapsed = _stopwatch.Elapsed;
        var leftBytes = Interlocked.Read(ref _leftBytes);
        var rightBytes = Interlocked.Read(ref _rightBytes);
        var discovered = Volatile.Read(ref _discoveredFiles);
        var completed = Volatile.Read(ref _completedFiles);

        var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        var leftRate = leftBytes / seconds;
        var rightRate = rightBytes / seconds;
        var filesPerSecond = completed / seconds;

        return new ComparisonProgressSnapshot(
            elapsed,
            leftBytes,
            rightBytes,
            discovered,
            completed,
            leftRate,
            rightRate,
            filesPerSecond);
    }

    /// <inheritdoc />
    public void FileDiscovered(string relativePath, long? leftSize, long? rightSize)
        => Interlocked.Increment(ref _discoveredFiles);

    /// <inheritdoc />
    public void FileCompleted(string relativePath, ComparisonStatus status)
        => Interlocked.Increment(ref _completedFiles);

    /// <inheritdoc />
    public void BytesRead(ComparisonSide side, long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        if (side == ComparisonSide.Left)
        {
            Interlocked.Add(ref _leftBytes, bytes);
        }
        else
        {
            Interlocked.Add(ref _rightBytes, bytes);
        }
    }
}

/// <summary>
/// Represents a snapshot of comparison progress metrics.
/// </summary>
/// <param name="Elapsed">Elapsed time since the comparison started.</param>
/// <param name="LeftBytes">Total bytes read from the left input.</param>
/// <param name="RightBytes">Total bytes read from the right input.</param>
/// <param name="DiscoveredFiles">Number of files discovered so far.</param>
/// <param name="CompletedFiles">Number of files processed so far.</param>
/// <param name="LeftBytesPerSecond">Bytes per second read from the left input.</param>
/// <param name="RightBytesPerSecond">Bytes per second read from the right input.</param>
/// <param name="FilesPerSecond">Files processed per second.</param>
public readonly record struct ComparisonProgressSnapshot(
    TimeSpan Elapsed,
    long LeftBytes,
    long RightBytes,
    int DiscoveredFiles,
    int CompletedFiles,
    double LeftBytesPerSecond,
    double RightBytesPerSecond,
    double FilesPerSecond)
{
    /// <summary>
    /// Gets the total bytes read across both inputs.
    /// </summary>
    public long TotalBytes => LeftBytes + RightBytes;

    /// <summary>
    /// Gets the number of files that have been discovered but not yet processed.
    /// </summary>
    public int PendingFiles => Math.Max(0, DiscoveredFiles - CompletedFiles);
}

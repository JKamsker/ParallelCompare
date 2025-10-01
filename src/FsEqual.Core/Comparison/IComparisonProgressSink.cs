using System;

namespace FsEqual.Core.Comparison;

/// <summary>
/// Identifies the side of the comparison that performed an IO operation.
/// </summary>
public enum ComparisonSide
{
    /// <summary>
    /// Data read from the left input.
    /// </summary>
    Left,

    /// <summary>
    /// Data read from the right input.
    /// </summary>
    Right
}

/// <summary>
/// Receives streaming progress updates during comparison execution.
/// </summary>
public interface IComparisonProgressSink
{
    /// <summary>
    /// Notifies the sink that a file has been discovered and queued for processing.
    /// </summary>
    /// <param name="relativePath">Path relative to the comparison root.</param>
    /// <param name="leftSize">Size of the left file, when available.</param>
    /// <param name="rightSize">Size of the right file, when available.</param>
    void FileDiscovered(string relativePath, long? leftSize, long? rightSize);

    /// <summary>
    /// Notifies the sink that a file has finished processing.
    /// </summary>
    /// <param name="relativePath">Path relative to the comparison root.</param>
    /// <param name="status">Final comparison status for the file.</param>
    void FileCompleted(string relativePath, ComparisonStatus status);

    /// <summary>
    /// Records the number of bytes read from one side of the comparison.
    /// </summary>
    /// <param name="side">Side that performed the read.</param>
    /// <param name="bytes">Number of bytes read.</param>
    void BytesRead(ComparisonSide side, long bytes);
}

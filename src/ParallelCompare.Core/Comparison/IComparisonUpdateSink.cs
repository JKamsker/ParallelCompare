namespace ParallelCompare.Core.Comparison;

/// <summary>
/// Receives streaming updates as comparison results are produced.
/// </summary>
public interface IComparisonUpdateSink
{
    /// <summary>
    /// Notifies the sink that a directory has been discovered.
    /// </summary>
    /// <param name="relativePath">Relative path of the directory.</param>
    /// <param name="name">Display name for the directory.</param>
    void DirectoryDiscovered(string relativePath, string name);

    /// <summary>
    /// Notifies the sink that a comparison node has been completed.
    /// </summary>
    /// <param name="node">The completed node.</param>
    void NodeCompleted(ComparisonNode node);
}

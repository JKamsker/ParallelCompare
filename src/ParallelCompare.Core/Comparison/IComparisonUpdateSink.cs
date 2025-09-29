namespace ParallelCompare.Core.Comparison;

public interface IComparisonUpdateSink
{
    void DirectoryDiscovered(string relativePath, string name);
    void NodeCompleted(ComparisonNode node);
}

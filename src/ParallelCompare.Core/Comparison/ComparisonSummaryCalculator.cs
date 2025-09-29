using ParallelCompare.Core.Options;

namespace ParallelCompare.Core.Comparison;

public static class ComparisonSummaryCalculator
{
    public static ComparisonSummary Calculate(ComparisonNode root)
    {
        var totals = (total: 0, equal: 0, different: 0, leftOnly: 0, rightOnly: 0, error: 0);
        Traverse(root);
        return new ComparisonSummary(totals.total, totals.equal, totals.different, totals.leftOnly, totals.rightOnly, totals.error);

        void Traverse(ComparisonNode node)
        {
            if (node.NodeType == ComparisonNodeType.File)
            {
                totals.total++;
                switch (node.Status)
                {
                    case ComparisonStatus.Equal:
                        totals.equal++;
                        break;
                    case ComparisonStatus.Different:
                        totals.different++;
                        break;
                    case ComparisonStatus.LeftOnly:
                        totals.leftOnly++;
                        break;
                    case ComparisonStatus.RightOnly:
                        totals.rightOnly++;
                        break;
                    case ComparisonStatus.Error:
                        totals.error++;
                        break;
                }
            }

            foreach (var child in node.Children)
            {
                Traverse(child);
            }
        }
    }
}

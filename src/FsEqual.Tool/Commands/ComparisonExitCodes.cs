using FsEqual.Tool.Comparison;

namespace FsEqual.Tool.Commands;

internal static class ComparisonExitCodes
{
    public static int Calculate(ComparisonOptions options, ComparisonResult result)
    {
        var summary = result.Summary;
        var hasDifferences = summary.FileDifferences > 0 || summary.MissingLeft > 0 || summary.MissingRight > 0 || summary.DirectoryDifferences > 0 || summary.BaselineDifferences > 0;
        var hasErrors = summary.Errors > 0;

        return options.FailOn switch
        {
            FailOnRule.Any when hasErrors => 2,
            FailOnRule.Any when hasDifferences => 1,
            FailOnRule.Differences when hasDifferences => 1,
            FailOnRule.Errors when hasErrors => 2,
            _ => 0
        };
    }
}

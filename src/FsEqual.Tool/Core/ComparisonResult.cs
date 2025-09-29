namespace FsEqual.Tool.Core;

internal sealed class ComparisonResult
{
    public ComparisonResult(
        ComparisonOutcome outcome,
        ComparisonSummary summary,
        IReadOnlyList<FileDifference> differences,
        IReadOnlyList<ComparisonError> errors,
        TimeSpan duration)
    {
        Outcome = outcome;
        Summary = summary;
        Differences = differences;
        Errors = errors;
        Duration = duration;
    }

    public ComparisonOutcome Outcome { get; }

    public ComparisonSummary Summary { get; }

    public IReadOnlyList<FileDifference> Differences { get; }

    public IReadOnlyList<ComparisonError> Errors { get; }

    public TimeSpan Duration { get; }
}

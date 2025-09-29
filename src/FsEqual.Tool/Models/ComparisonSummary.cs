namespace FsEqual.Tool.Models;

public sealed class ComparisonSummary
{
    public int TotalItems { get; set; }
    public int Directories { get; set; }
    public int Files { get; set; }
    public int Equal { get; set; }
    public int Differences { get; set; }
    public int MissingLeft { get; set; }
    public int MissingRight { get; set; }
    public int Errors { get; set; }
    public TimeSpan Duration { get; set; }
}

namespace FsEqual.Tool.Core;

internal sealed class ComparisonSummary
{
    public int FilesCompared { get; set; }

    public int DirectoriesCompared { get; set; }

    public int EqualFiles { get; set; }

    public int EqualDirectories { get; set; }

    public int MissingLeft { get; set; }

    public int MissingRight { get; set; }

    public int TypeMismatches { get; set; }

    public int SizeMismatches { get; set; }

    public int HashMismatches { get; set; }

    public int MetadataMismatches { get; set; }

    public int ErrorCount { get; set; }
}

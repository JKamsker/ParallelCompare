using FsEqual.Core.Comparison;
using FsEqual.Core.Options;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Provides contextual information shared by all report exporters.
/// </summary>
public sealed class ReportExportContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExportContext"/> class.
    /// </summary>
    /// <param name="result">Comparison result to export.</param>
    /// <param name="settings">Resolved settings used for the comparison.</param>
    /// <param name="document">Structured representation of the export document.</param>
    /// <param name="destination">Destination file path.</param>
    public ReportExportContext(
        ComparisonResult result,
        ResolvedCompareSettings settings,
        ReportDocument document,
        string destination)
    {
        Result = result;
        Settings = settings;
        Document = document;
        Destination = destination;
    }

    /// <summary>
    /// Gets the comparison result to export.
    /// </summary>
    public ComparisonResult Result { get; }

    /// <summary>
    /// Gets the resolved settings used for the run.
    /// </summary>
    public ResolvedCompareSettings Settings { get; }

    /// <summary>
    /// Gets the structured export document shared by all exporters.
    /// </summary>
    public ReportDocument Document { get; }

    /// <summary>
    /// Gets the destination file path supplied for the export.
    /// </summary>
    public string Destination { get; }

    /// <summary>
    /// Gets the preferred hash algorithm for textual exports when available.
    /// </summary>
    public string? PreferredAlgorithm => Document.Metadata.PrimaryAlgorithm;
}

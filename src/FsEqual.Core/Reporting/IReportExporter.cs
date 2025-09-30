using System.Threading;
using System.Threading.Tasks;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Defines the contract implemented by report exporters.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Gets the canonical format identifier handled by the exporter.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Writes the comparison result to the requested destination.
    /// </summary>
    /// <param name="context">Context describing the export request.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task ExportAsync(ReportExportContext context, CancellationToken cancellationToken);
}

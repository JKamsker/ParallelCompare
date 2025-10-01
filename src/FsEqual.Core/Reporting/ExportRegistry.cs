using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FsEqual.Core.Comparison;
using FsEqual.Core.Options;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Coordinates registered exporters and executes them for requested formats.
/// </summary>
public sealed class ExportRegistry
{
    private readonly Dictionary<string, IReportExporter> _exporters;
    private readonly ReportDocumentBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportRegistry"/> class with default exporters.
    /// </summary>
    public ExportRegistry()
        : this(new IReportExporter[]
        {
            new JsonReportExporter(),
            new SummaryReportExporter(),
            new CsvReportExporter(),
            new MarkdownReportExporter(),
            new HtmlReportExporter()
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportRegistry"/> class.
    /// </summary>
    /// <param name="exporters">Exporters that should be available.</param>
    public ExportRegistry(IEnumerable<IReportExporter> exporters)
    {
        _builder = new ReportDocumentBuilder();
        _exporters = new Dictionary<string, IReportExporter>(StringComparer.OrdinalIgnoreCase);
        foreach (var exporter in exporters)
        {
            Register(exporter);
        }
    }

    /// <summary>
    /// Registers or replaces the exporter for the specified format.
    /// </summary>
    /// <param name="exporter">Exporter to register.</param>
    public void Register(IReportExporter exporter)
    {
        _exporters[exporter.Format] = exporter;
    }

    /// <summary>
    /// Executes the requested exports.
    /// </summary>
    /// <param name="result">Comparison result to export.</param>
    /// <param name="settings">Resolved settings used for the run.</param>
    /// <param name="requests">Export requests keyed by format.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task ExportAsync(
        ComparisonResult result,
        ResolvedCompareSettings settings,
        IEnumerable<ReportExportRequest> requests,
        CancellationToken cancellationToken)
    {
        var materialized = requests
            .Where(static request => !string.IsNullOrWhiteSpace(request.Path))
            .Select(request => request with
            {
                Format = request.Format.ToLowerInvariant(),
                Path = Path.GetFullPath(request.Path)
            })
            .ToList();

        if (materialized.Count == 0)
        {
            return;
        }

        var document = _builder.Build(result, settings);

        foreach (var request in materialized)
        {
            if (!_exporters.TryGetValue(request.Format, out var exporter))
            {
                throw new InvalidOperationException($"No exporter registered for format '{request.Format}'.");
            }

            var context = new ReportExportContext(result, settings, document, request.Path);
            await exporter.ExportAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }
}

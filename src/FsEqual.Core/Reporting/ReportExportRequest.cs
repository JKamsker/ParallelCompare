namespace FsEqual.Core.Reporting;

/// <summary>
/// Represents a single export request containing the target format and destination path.
/// </summary>
/// <param name="Format">Canonical export format identifier.</param>
/// <param name="Path">Destination file path.</param>
public sealed record ReportExportRequest(string Format, string Path);

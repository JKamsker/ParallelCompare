using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FsEqual.Core.Comparison;

namespace FsEqual.Core.Reporting;

/// <summary>
/// Provides shared string helpers used by textual report exporters.
/// </summary>
public static class ReportTextUtilities
{
    /// <summary>
    /// Normalizes a comparison node path using forward slashes.
    /// </summary>
    /// <param name="node">Node to normalize.</param>
    /// <returns>The normalized relative path.</returns>
    public static string GetNodePath(ComparisonNode node)
    {
        if (string.IsNullOrWhiteSpace(node.RelativePath))
        {
            return node.Name;
        }

        return node.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Escapes a CSV field, quoting when necessary.
    /// </summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <summary>
    /// Escapes Markdown-sensitive characters.
    /// </summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapeMarkdown(string value)
        => value.Replace("|", "\\|").Replace("`", "\\`");

    /// <summary>
    /// Selects a hash value for textual exports.
    /// </summary>
    /// <param name="hashes">Hash dictionary keyed by algorithm name.</param>
    /// <param name="preferred">Preferred algorithm name.</param>
    /// <returns>The selected hash value or <c>null</c>.</returns>
    public static string? SelectHash(IReadOnlyDictionary<string, string>? hashes, string? preferred)
    {
        if (hashes is null || hashes.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferred) && hashes.TryGetValue(preferred, out var value))
        {
            return value;
        }

        return hashes
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => pair.Value)
            .FirstOrDefault();
    }
}

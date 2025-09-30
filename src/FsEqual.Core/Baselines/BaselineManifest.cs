using System.Collections.Immutable;
using FsEqual.Core.Options;

namespace FsEqual.Core.Baselines;

/// <summary>
/// Describes the metadata captured for a baseline snapshot, including the algorithms and
/// ignore patterns that were active when it was recorded.
/// </summary>
/// <param name="Version">Schema version used to persist the manifest.</param>
/// <param name="SourcePath">Root path that the baseline represents.</param>
/// <param name="CreatedAt">Timestamp for when the snapshot was created.</param>
/// <param name="IgnorePatterns">File patterns that were ignored during capture.</param>
/// <param name="CaseSensitive">Whether comparisons were case sensitive.</param>
/// <param name="ModifiedTimeTolerance">Allowed modified time tolerance during comparisons.</param>
/// <param name="Algorithms">Hash algorithms stored for files in the baseline.</param>
/// <param name="Root">Root entry describing the captured file tree.</param>
public sealed record BaselineManifest(
    string Version,
    string SourcePath,
    DateTimeOffset CreatedAt,
    ImmutableArray<string> IgnorePatterns,
    bool CaseSensitive,
    TimeSpan? ModifiedTimeTolerance,
    ImmutableArray<HashAlgorithmType> Algorithms,
    BaselineEntry Root
)
{
    /// <summary>
    /// Current manifest version understood by the application.
    /// </summary>
    public const string CurrentVersion = "1.0";
}

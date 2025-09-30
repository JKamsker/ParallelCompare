using System;
using System.Diagnostics.CodeAnalysis;

namespace FsEqual.Core.FileSystem.Providers;

/// <summary>
/// Placeholder SSH provider documenting the remote comparison roadmap.
/// </summary>
public sealed class SshFileSystemProvider : IFileSystemProvider
{
    private const string RoadmapMessage = "SSH remotes are part of the Phase 6 remote comparison roadmap and are not yet implemented.";

    /// <inheritdoc />
    public bool TryResolve(string source, [NotNullWhen(true)] out FileSystemResolution? resolution)
    {
        resolution = null;

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, "ssh", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new NotImplementedException(RoadmapMessage);
    }
}

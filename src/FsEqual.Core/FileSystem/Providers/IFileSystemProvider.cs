using System.Diagnostics.CodeAnalysis;

namespace FsEqual.Core.FileSystem.Providers;

/// <summary>
/// Resolves file system abstractions for comparison sources.
/// </summary>
public interface IFileSystemProvider
{
    /// <summary>
    /// Attempts to resolve a file system for the specified source path or URI.
    /// </summary>
    /// <param name="source">Source path provided by the user.</param>
    /// <param name="resolution">Resolved file system and normalized path.</param>
    /// <returns><c>true</c> when the provider handled the source; otherwise, <c>false</c>.</returns>
    bool TryResolve(string source, [NotNullWhen(true)] out FileSystemResolution? resolution);
}

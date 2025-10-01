using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace FsEqual.Core.FileSystem.Providers;

/// <summary>
/// Resolves local file system paths to the physical file system implementation.
/// </summary>
public sealed class PhysicalFileSystemProvider : IFileSystemProvider
{
    /// <inheritdoc />
    public bool TryResolve(string source, [NotNullWhen(true)] out FileSystemResolution? resolution)
    {
        resolution = null;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                return false;
            }

            source = uri.LocalPath;
        }

        var fullPath = Path.GetFullPath(source);
        resolution = new FileSystemResolution(fullPath, PhysicalFileSystem.Instance);
        return true;
    }
}

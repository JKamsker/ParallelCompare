using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace FsEqual.Core.FileSystem.Providers;

/// <summary>
/// Maintains the ordered set of file system providers used to resolve comparison sources.
/// </summary>
public sealed class FileSystemProviderCatalog
{
    private readonly ImmutableArray<IFileSystemProvider> _providers;

    private FileSystemProviderCatalog(ImmutableArray<IFileSystemProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>
    /// Gets the default catalog containing built-in providers.
    /// </summary>
    public static FileSystemProviderCatalog Default { get; } = CreateDefault();

    /// <summary>
    /// Resolves a source string to a file system and normalized path.
    /// </summary>
    /// <param name="source">Source path or URI supplied by the user.</param>
    /// <returns>The resolved file system information.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no provider can resolve the source.</exception>
    public FileSystemResolution Resolve(string source)
    {
        foreach (var provider in _providers)
        {
            if (provider.TryResolve(source, out var resolution))
            {
                return resolution;
            }
        }

        throw new InvalidOperationException($"No file system provider could resolve source '{source}'.");
    }

    private static FileSystemProviderCatalog CreateDefault()
    {
        var providers = ImmutableArray.Create<IFileSystemProvider>(
            new SshFileSystemProvider(),
            new PhysicalFileSystemProvider());

        return new FileSystemProviderCatalog(providers);
    }

    /// <summary>
    /// Creates a catalog with the specified providers.
    /// </summary>
    /// <param name="providers">Providers ordered by precedence.</param>
    /// <returns>The catalog instance.</returns>
    public static FileSystemProviderCatalog Create(IEnumerable<IFileSystemProvider> providers)
        => new(providers.ToImmutableArray());
}

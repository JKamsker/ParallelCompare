using FsEqual.Core.FileSystem;

namespace FsEqual.Core.FileSystem.Providers;

/// <summary>
/// Represents the result of resolving a source into a file system abstraction.
/// </summary>
/// <param name="Path">Normalized path consumed by the file system.</param>
/// <param name="FileSystem">Resolved file system implementation.</param>
public sealed record FileSystemResolution(string Path, IFileSystem FileSystem);

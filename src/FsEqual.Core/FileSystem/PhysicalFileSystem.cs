using System;
using System.Collections.Generic;
using System.IO;

namespace FsEqual.Core.FileSystem;

/// <summary>
/// Real file system implementation backed by <see cref="System.IO"/>.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <summary>
    /// Gets the shared singleton instance of the physical file system.
    /// </summary>
    public static PhysicalFileSystem Instance { get; } = new();

    private PhysicalFileSystem()
    {
    }

    /// <inheritdoc />
    public IDirectoryEntry GetDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        return new PhysicalDirectoryEntry(info);
    }

    /// <inheritdoc />
    public IFileEntry GetFile(string path)
    {
        var info = new FileInfo(path);
        return new PhysicalFileEntry(info);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    private sealed class PhysicalDirectoryEntry : IDirectoryEntry
    {
        private readonly DirectoryInfo _info;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalDirectoryEntry"/> class.
        /// </summary>
        /// <param name="info">Directory information backing the entry.</param>
        public PhysicalDirectoryEntry(DirectoryInfo info)
        {
            _info = info;
        }

        /// <inheritdoc />
        public string Name => _info.Name;

        /// <inheritdoc />
        public string FullPath => _info.FullName;

        /// <inheritdoc />
        public FileSystemEntryType EntryType => FileSystemEntryType.Directory;

        /// <inheritdoc />
        public bool Exists => _info.Exists;

        /// <inheritdoc />
        public DateTimeOffset LastWriteTimeUtc => _info.LastWriteTimeUtc;

        /// <inheritdoc />
        public IEnumerable<IFileSystemEntry> EnumerateEntries()
        {
            if (!_info.Exists)
            {
                yield break;
            }

            foreach (var entry in _info.EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo directory)
                {
                    yield return new PhysicalDirectoryEntry(directory);
                }
                else if (entry is FileInfo file)
                {
                    yield return new PhysicalFileEntry(file);
                }
            }
        }
    }

    private sealed class PhysicalFileEntry : IFileEntry
    {
        private readonly FileInfo _info;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalFileEntry"/> class.
        /// </summary>
        /// <param name="info">File information backing the entry.</param>
        public PhysicalFileEntry(FileInfo info)
        {
            _info = info;
        }

        /// <inheritdoc />
        public string Name => _info.Name;

        /// <inheritdoc />
        public string FullPath => _info.FullName;

        /// <inheritdoc />
        public FileSystemEntryType EntryType => FileSystemEntryType.File;

        /// <inheritdoc />
        public bool Exists => _info.Exists;

        /// <inheritdoc />
        public DateTimeOffset LastWriteTimeUtc => _info.LastWriteTimeUtc;

        /// <inheritdoc />
        public long Length => _info.Length;

        /// <inheritdoc />
        public Stream OpenRead() => _info.OpenRead();
    }
}

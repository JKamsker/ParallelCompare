using System;
using System.Collections.Generic;
using System.IO;

namespace ParallelCompare.Core.FileSystem;

public sealed class PhysicalFileSystem : IFileSystem
{
    public static PhysicalFileSystem Instance { get; } = new();

    private PhysicalFileSystem()
    {
    }

    public IDirectoryEntry GetDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        return new PhysicalDirectoryEntry(info);
    }

    public IFileEntry GetFile(string path)
    {
        var info = new FileInfo(path);
        return new PhysicalFileEntry(info);
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    private sealed class PhysicalDirectoryEntry : IDirectoryEntry
    {
        private readonly DirectoryInfo _info;

        public PhysicalDirectoryEntry(DirectoryInfo info)
        {
            _info = info;
        }

        public string Name => _info.Name;

        public string FullPath => _info.FullName;

        public FileSystemEntryType EntryType => FileSystemEntryType.Directory;

        public bool Exists => _info.Exists;

        public DateTimeOffset LastWriteTimeUtc => _info.LastWriteTimeUtc;

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

        public PhysicalFileEntry(FileInfo info)
        {
            _info = info;
        }

        public string Name => _info.Name;

        public string FullPath => _info.FullName;

        public FileSystemEntryType EntryType => FileSystemEntryType.File;

        public bool Exists => _info.Exists;

        public DateTimeOffset LastWriteTimeUtc => _info.LastWriteTimeUtc;

        public long Length => _info.Length;

        public Stream OpenRead() => _info.OpenRead();
    }
}

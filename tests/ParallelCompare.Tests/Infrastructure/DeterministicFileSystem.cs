using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ParallelCompare.Core.FileSystem;

namespace ParallelCompare.Tests.Infrastructure;

internal sealed class DeterministicFileSystem : IFileSystem
{
    private readonly string _rootPath;
    private readonly Dictionary<string, DirectoryNode> _directories;
    private readonly Dictionary<string, FileNode> _files;
    private readonly bool _caseSensitive;

    private DeterministicFileSystem(string rootPath, DirectoryNode root, bool caseSensitive)
    {
        _rootPath = NormalizeDirectory(rootPath);
        _caseSensitive = caseSensitive;
        _directories = new Dictionary<string, DirectoryNode>(CreateComparer(caseSensitive))
        {
            [_rootPath] = root
        };
        _files = new Dictionary<string, FileNode>(CreateComparer(caseSensitive));
        IndexNodes(_rootPath, root);
    }

    public static DeterministicFileSystem FromTemplate(string rootPath, DeterministicFileSystemTemplate template, bool caseSensitive)
        => new(rootPath, template.Build(caseSensitive, Path.GetFileName(NormalizeDirectory(rootPath))), caseSensitive);

    public static DeterministicFileSystem Compose(IEnumerable<(string Path, DeterministicFileSystemTemplate Template)> mounts, bool caseSensitive)
    {
        var comparer = CreateComparer(caseSensitive);
        var root = new DirectoryNode("root", DateTimeOffset.UnixEpoch, comparer);

        foreach (var (path, template) in mounts)
        {
            var normalized = NormalizeDirectory(path);
            var segments = normalized.TrimStart(Path.DirectorySeparatorChar)
                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                root = template.Build(caseSensitive, "root");
                continue;
            }

            var current = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i];
                if (!current.Directories.TryGetValue(segment, out var next))
                {
                    next = new DirectoryNode(segment, DateTimeOffset.UnixEpoch, comparer);
                    current.Directories[segment] = next;
                }

                current = next;
            }

            var leafName = segments[^1];
            current.Directories[leafName] = template.Build(caseSensitive, leafName);
        }

        return new DeterministicFileSystem(Path.DirectorySeparatorChar.ToString(), root, caseSensitive);
    }

    public IDirectoryEntry GetDirectory(string path)
    {
        var normalized = NormalizeDirectory(path);
        if (_directories.TryGetValue(normalized, out var node))
        {
            return new DirectoryEntry(node, normalized, this);
        }

        return new MissingDirectoryEntry(Path.GetFileName(normalized), normalized);
    }

    public IFileEntry GetFile(string path)
    {
        var normalized = NormalizeFile(path);
        if (_files.TryGetValue(normalized, out var node))
        {
            return new FileEntry(node, normalized);
        }

        return new MissingFileEntry(Path.GetFileName(normalized), normalized);
    }

    public bool DirectoryExists(string path) => _directories.ContainsKey(NormalizeDirectory(path));

    public bool FileExists(string path) => _files.ContainsKey(NormalizeFile(path));

    private void IndexNodes(string currentPath, DirectoryNode node)
    {
        foreach (var (name, directory) in node.Directories)
        {
            var childPath = NormalizeDirectory(Path.Combine(currentPath, name));
            _directories[childPath] = directory;
            IndexNodes(childPath, directory);
        }

        foreach (var (name, file) in node.Files)
        {
            var filePath = NormalizeFile(Path.Combine(currentPath, name));
            _files[filePath] = file;
        }
    }

    private static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length > 1 && full.EndsWith(Path.DirectorySeparatorChar))
        {
            full = full.TrimEnd(Path.DirectorySeparatorChar);
        }

        return full;
    }

    private static string NormalizeFile(string path) => Path.GetFullPath(path);

    private static StringComparer CreateComparer(bool caseSensitive)
        => caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private sealed class DirectoryEntry : IDirectoryEntry
    {
        private readonly DirectoryNode _node;
        private readonly string _path;
        private readonly DeterministicFileSystem _fileSystem;

        public DirectoryEntry(DirectoryNode node, string path, DeterministicFileSystem fileSystem)
        {
            _node = node;
            _path = path;
            _fileSystem = fileSystem;
        }

        public string Name => _node.Name;

        public string FullPath => _path;

        public FileSystemEntryType EntryType => FileSystemEntryType.Directory;

        public bool Exists => true;

        public DateTimeOffset LastWriteTimeUtc => _node.LastWriteTimeUtc;

        public IEnumerable<IFileSystemEntry> EnumerateEntries()
        {
            foreach (var directory in _node.Directories.Values)
            {
                var childPath = NormalizeDirectory(Path.Combine(_path, directory.Name));
                yield return new DirectoryEntry(directory, childPath, _fileSystem);
            }

            foreach (var file in _node.Files.Values)
            {
                var childPath = NormalizeFile(Path.Combine(_path, file.Name));
                yield return new FileEntry(file, childPath);
            }
        }
    }

    private sealed class FileEntry : IFileEntry
    {
        private readonly FileNode _node;
        private readonly string _path;

        public FileEntry(FileNode node, string path)
        {
            _node = node;
            _path = path;
        }

        public string Name => _node.Name;

        public string FullPath => _path;

        public FileSystemEntryType EntryType => FileSystemEntryType.File;

        public bool Exists => true;

        public DateTimeOffset LastWriteTimeUtc => _node.LastWriteTimeUtc;

        public long Length => _node.Length;

        public Stream OpenRead() => _node.OpenRead();
    }

    private sealed class MissingDirectoryEntry : IDirectoryEntry
    {
        public MissingDirectoryEntry(string name, string path)
        {
            Name = string.IsNullOrEmpty(name) ? path : name;
            FullPath = path;
        }

        public string Name { get; }

        public string FullPath { get; }

        public FileSystemEntryType EntryType => FileSystemEntryType.Directory;

        public bool Exists => false;

        public DateTimeOffset LastWriteTimeUtc => DateTimeOffset.MinValue;

        public IEnumerable<IFileSystemEntry> EnumerateEntries()
        {
            yield break;
        }
    }

    private sealed class MissingFileEntry : IFileEntry
    {
        public MissingFileEntry(string name, string path)
        {
            Name = string.IsNullOrEmpty(name) ? path : name;
            FullPath = path;
        }

        public string Name { get; }

        public string FullPath { get; }

        public FileSystemEntryType EntryType => FileSystemEntryType.File;

        public bool Exists => false;

        public DateTimeOffset LastWriteTimeUtc => DateTimeOffset.MinValue;

        public long Length => 0;

        public Stream OpenRead() => Stream.Null;
    }

    internal sealed class DirectoryNode
    {
        public DirectoryNode(string name, DateTimeOffset lastWriteTimeUtc, StringComparer comparer)
        {
            Name = name;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Directories = new Dictionary<string, DirectoryNode>(comparer);
            Files = new Dictionary<string, FileNode>(comparer);
        }

        public string Name { get; }

        public DateTimeOffset LastWriteTimeUtc { get; }

        public Dictionary<string, DirectoryNode> Directories { get; }

        public Dictionary<string, FileNode> Files { get; }
    }

    internal abstract class FileNode
    {
        protected FileNode(string name, DateTimeOffset lastWriteTimeUtc)
        {
            Name = name;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public string Name { get; }

        public DateTimeOffset LastWriteTimeUtc { get; }

        public abstract long Length { get; }

        public abstract Stream OpenRead();
    }

    internal sealed class TextFileNode : FileNode
    {
        private readonly byte[] _data;

        public TextFileNode(string name, DateTimeOffset lastWriteTimeUtc, string content)
            : base(name, lastWriteTimeUtc)
        {
            _data = Encoding.UTF8.GetBytes(content ?? string.Empty);
        }

        public override long Length => _data.LongLength;

        public override Stream OpenRead() => new MemoryStream(_data, writable: false);
    }

    internal sealed class BinaryFileNode : FileNode
    {
        private readonly int _seed;
        private readonly int _size;

        public BinaryFileNode(string name, DateTimeOffset lastWriteTimeUtc, int seed, int size)
            : base(name, lastWriteTimeUtc)
        {
            _seed = seed;
            _size = size;
        }

        public override long Length => _size;

        public override Stream OpenRead()
        {
            var data = new byte[_size];
            var random = new Random(_seed);
            random.NextBytes(data);
            return new MemoryStream(data, writable: false);
        }
    }

    public sealed class DeterministicFileSystemTemplate
    {
        private readonly DirectoryDefinition _root;

        private DeterministicFileSystemTemplate(DirectoryDefinition root)
        {
            _root = root;
        }

        public static DeterministicFileSystemTemplate FromJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = ParseDirectory(document.RootElement, "root");
            return new DeterministicFileSystemTemplate(root);
        }

        public DeterministicFileSystemTemplate Clone()
            => new(new DirectoryDefinition(_root));

        public void RemoveEntry(string relativePath)
        {
            var segments = Split(relativePath);
            RemoveEntry(_root, segments, 0);
        }

        public void UpdateTextFile(string relativePath, string content)
        {
            var file = GetFileDefinition(relativePath);
            if (file is TextFileDefinition text)
            {
                text.Content = content;
                return;
            }

            throw new InvalidOperationException($"File '{relativePath}' is not a text file.");
        }

        public void UpdateBinaryFile(string relativePath, int? seed = null, int? size = null)
        {
            var file = GetFileDefinition(relativePath);
            if (file is BinaryFileDefinition binary)
            {
                if (seed.HasValue)
                {
                    binary.Seed = seed.Value;
                }

                if (size.HasValue)
                {
                    binary.Size = size.Value;
                }

                return;
            }

            throw new InvalidOperationException($"File '{relativePath}' is not a binary file.");
        }

        public DeterministicFileSystem BuildFileSystem(string rootPath, bool caseSensitive)
            => DeterministicFileSystem.FromTemplate(rootPath, this, caseSensitive);

        internal DirectoryNode Build(bool caseSensitive, string? overrideName = null)
        {
            var comparer = CreateComparer(caseSensitive);
            return BuildDirectory(_root, comparer, overrideName ?? _root.Name);
        }

        private static DirectoryNode BuildDirectory(DirectoryDefinition definition, StringComparer comparer, string? overrideName = null)
        {
            var node = new DirectoryNode(overrideName ?? definition.Name, definition.LastWriteTimeUtc, comparer);
            foreach (var (name, directory) in definition.Directories)
            {
                node.Directories[name] = BuildDirectory(directory, comparer);
            }

            foreach (var (name, file) in definition.Files)
            {
                node.Files[name] = file.ToNode(name);
            }

            return node;
        }

        private static DirectoryDefinition ParseDirectory(JsonElement element, string name)
        {
            var modified = element.TryGetProperty("modified", out var modifiedElement)
                ? modifiedElement.GetDateTimeOffset()
                : DateTimeOffset.UnixEpoch;

            var directory = new DirectoryDefinition(name, modified);

            if (element.TryGetProperty("directories", out var directoriesElement))
            {
                foreach (var child in directoriesElement.EnumerateObject())
                {
                    directory.Directories[child.Name] = ParseDirectory(child.Value, child.Name);
                }
            }

            if (element.TryGetProperty("files", out var filesElement))
            {
                foreach (var child in filesElement.EnumerateObject())
                {
                    directory.Files[child.Name] = ParseFile(child.Value);
                }
            }

            return directory;
        }

        private static FileDefinition ParseFile(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return new TextFileDefinition(element.GetString() ?? string.Empty, DateTimeOffset.UnixEpoch);
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Unsupported file definition.");
            }

            var modified = element.TryGetProperty("modified", out var modifiedElement)
                ? modifiedElement.GetDateTimeOffset()
                : DateTimeOffset.UnixEpoch;

            if (element.TryGetProperty("content", out var contentElement))
            {
                return new TextFileDefinition(contentElement.GetString() ?? string.Empty, modified);
            }

            var seed = element.GetProperty("seed").GetInt32();
            var size = element.GetProperty("size").GetInt32();
            return new BinaryFileDefinition(seed, size, modified);
        }

        private static void RemoveEntry(DirectoryDefinition directory, IReadOnlyList<string> segments, int index)
        {
            if (index >= segments.Count)
            {
                return;
            }

            var key = segments[index];

            if (index == segments.Count - 1)
            {
                directory.Files.Remove(key);
                directory.Directories.Remove(key);
                return;
            }

            if (directory.Directories.TryGetValue(key, out var child))
            {
                RemoveEntry(child, segments, index + 1);
            }
        }

        private FileDefinition GetFileDefinition(string relativePath)
        {
            var segments = Split(relativePath);
            var directory = _root;
            for (var i = 0; i < segments.Count - 1; i++)
            {
                if (!directory.Directories.TryGetValue(segments[i], out var next))
                {
                    throw new InvalidOperationException($"Directory '{relativePath}' does not exist.");
                }

                directory = next;
            }

            var fileName = segments[^1];
            if (!directory.Files.TryGetValue(fileName, out var file))
            {
                throw new InvalidOperationException($"File '{relativePath}' does not exist.");
            }

            return file;
        }

        private static List<string> Split(string relativePath)
        {
            var segments = relativePath
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (segments.Count == 0)
            {
                throw new InvalidOperationException("Relative path must contain at least one segment.");
            }

            return segments;
        }

        private sealed class DirectoryDefinition
        {
            public DirectoryDefinition(string name, DateTimeOffset lastWriteTimeUtc)
            {
                Name = name;
                LastWriteTimeUtc = lastWriteTimeUtc;
                Directories = new Dictionary<string, DirectoryDefinition>(StringComparer.OrdinalIgnoreCase);
                Files = new Dictionary<string, FileDefinition>(StringComparer.OrdinalIgnoreCase);
            }

            public DirectoryDefinition(DirectoryDefinition other)
            {
                Name = other.Name;
                LastWriteTimeUtc = other.LastWriteTimeUtc;
                Directories = new Dictionary<string, DirectoryDefinition>(other.Directories.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in other.Directories)
                {
                    Directories[key] = new DirectoryDefinition(value);
                }

                Files = new Dictionary<string, FileDefinition>(other.Files.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in other.Files)
                {
                    Files[key] = value.Clone();
                }
            }

            public string Name { get; }

            public DateTimeOffset LastWriteTimeUtc { get; }

            public Dictionary<string, DirectoryDefinition> Directories { get; }

            public Dictionary<string, FileDefinition> Files { get; }
        }

        private abstract class FileDefinition
        {
            protected FileDefinition(DateTimeOffset lastWriteTimeUtc)
            {
                LastWriteTimeUtc = lastWriteTimeUtc;
            }

            public DateTimeOffset LastWriteTimeUtc { get; protected set; }

            public abstract FileNode ToNode(string name);

            public abstract FileDefinition Clone();
        }

        private sealed class TextFileDefinition : FileDefinition
        {
            public TextFileDefinition(string content, DateTimeOffset modified)
                : base(modified)
            {
                Content = content;
            }

            public string Content { get; set; }

            public override FileNode ToNode(string name) => new TextFileNode(name, LastWriteTimeUtc, Content);

            public override FileDefinition Clone() => new TextFileDefinition(Content, LastWriteTimeUtc);
        }

        private sealed class BinaryFileDefinition : FileDefinition
        {
            public BinaryFileDefinition(int seed, int size, DateTimeOffset modified)
                : base(modified)
            {
                Seed = seed;
                Size = size;
            }

            public int Seed { get; set; }

            public int Size { get; set; }

            public override FileNode ToNode(string name) => new BinaryFileNode(name, LastWriteTimeUtc, Seed, Size);

            public override FileDefinition Clone() => new BinaryFileDefinition(Seed, Size, LastWriteTimeUtc);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace FsEqual.Core.Comparison;

/// <summary>
/// Captures streaming comparison updates and publishes snapshot-friendly data for the interactive UI.
/// </summary>
public sealed class ComparisonTreeUpdateAdapter : IComparisonUpdateSink
{
    private readonly object _sync = new();
    private readonly Dictionary<string, MutableEntry> _entries;
    private readonly IEqualityComparer<string> _pathComparer;
    private readonly IComparer<string> _nameComparer;
    private string? _rootKey;
    private string? _pendingPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComparisonTreeUpdateAdapter"/> class.
    /// </summary>
    /// <param name="caseSensitive">Indicates whether relative paths should be treated as case sensitive.</param>
    public ComparisonTreeUpdateAdapter(bool caseSensitive = false)
    {
        _pathComparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _nameComparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _entries = new Dictionary<string, MutableEntry>(_pathComparer);
    }

    /// <summary>
    /// Gets the latest snapshot emitted from the adapter, if any.
    /// </summary>
    public ComparisonTreeSnapshot? LatestSnapshot { get; private set; }

    /// <summary>
    /// Occurs when the snapshot representation is updated.
    /// </summary>
    public event Action<ComparisonTreeSnapshot>? TreeUpdated;

    /// <summary>
    /// Occurs when a specific node update is available.
    /// </summary>
    public event Action<ComparisonNode>? NodeUpdated;

    /// <summary>
    /// Signals that a directory has been discovered during comparison.
    /// </summary>
    /// <param name="relativePath">Directory path relative to the comparison root.</param>
    /// <param name="name">Display name for the directory.</param>
    public void DirectoryDiscovered(string relativePath, string name)
    {
        lock (_sync)
        {
            var normalized = Normalize(relativePath);
            var entry = GetOrCreateEntry(normalized, name, ComparisonNodeType.Directory);
            entry.NodeType = ComparisonNodeType.Directory;
            entry.Detail = null;
            entry.ExplicitStatus = null;
            AttachToParent(entry);
            _pendingPath = normalized;
            EmitSnapshot();
        }
    }

    /// <summary>
    /// Signals that a comparison node has finished processing.
    /// </summary>
    /// <param name="node">The completed node.</param>
    public void NodeCompleted(ComparisonNode node)
    {
        lock (_sync)
        {
            var normalized = Normalize(node.RelativePath);
            var entry = GetOrCreateEntry(normalized, node.Name, node.NodeType);
            entry.NodeType = node.NodeType;
            entry.Detail = node.NodeType == ComparisonNodeType.File ? node.Detail : null;
            entry.ExplicitStatus = node.Status;
            entry.IsCompleted = true;
            AttachToParent(entry);

            if (node.NodeType == ComparisonNodeType.Directory)
            {
                foreach (var child in node.Children)
                {
                    var childPath = Normalize(child.RelativePath);
                    var childEntry = GetOrCreateEntry(childPath, child.Name, child.NodeType);
                    childEntry.NodeType = child.NodeType;
                    if (child.NodeType == ComparisonNodeType.File)
                    {
                        childEntry.Detail ??= child.Detail;
                    }

                    childEntry.ExplicitStatus ??= child.Status;
                    AttachToParent(childEntry);
                }
            }

            _pendingPath = normalized;
            EmitSnapshot();
        }
    }

    private void EmitSnapshot()
    {
        if (_rootKey is null)
        {
            return;
        }

        if (!_entries.TryGetValue(_rootKey, out var rootEntry))
        {
            return;
        }

        var rootNode = BuildSnapshot(rootEntry);
        var lookupBuilder = ImmutableDictionary.CreateBuilder<string, ComparisonNode>(_pathComparer);
        PopulateLookup(rootNode, lookupBuilder);

        var snapshot = new ComparisonTreeSnapshot(rootNode, lookupBuilder.ToImmutable());
        LatestSnapshot = snapshot;
        TreeUpdated?.Invoke(snapshot);

        if (_pendingPath is { } path && snapshot.Nodes.TryGetValue(path, out var updated))
        {
            NodeUpdated?.Invoke(updated);
        }

        _pendingPath = null;
    }

    private ComparisonNode BuildSnapshot(MutableEntry entry)
    {
        var childNodes = entry.Children
            .Select(childPath => _entries[childPath])
            .OrderBy(child => child.Name, _nameComparer)
            .Select(BuildSnapshot)
            .ToImmutableArray();

        var status = entry.NodeType == ComparisonNodeType.Directory
            ? entry.ExplicitStatus ?? DetermineDirectoryStatus(childNodes)
            : entry.ExplicitStatus ?? ComparisonStatus.Equal;

        var detail = entry.NodeType == ComparisonNodeType.File ? entry.Detail : null;

        return new ComparisonNode(
            entry.Name,
            entry.RelativePath,
            entry.NodeType,
            status,
            detail,
            childNodes);
    }

    private static ComparisonStatus DetermineDirectoryStatus(IEnumerable<ComparisonNode> children)
    {
        var hasError = false;
        var hasDifferent = false;
        var hasLeftOnly = false;
        var hasRightOnly = false;

        foreach (var child in children)
        {
            switch (child.Status)
            {
                case ComparisonStatus.Error:
                    hasError = true;
                    break;
                case ComparisonStatus.Different:
                    hasDifferent = true;
                    break;
                case ComparisonStatus.LeftOnly:
                    hasLeftOnly = true;
                    break;
                case ComparisonStatus.RightOnly:
                    hasRightOnly = true;
                    break;
            }
        }

        if (hasError)
        {
            return ComparisonStatus.Error;
        }

        if (hasDifferent || (hasLeftOnly && hasRightOnly))
        {
            return ComparisonStatus.Different;
        }

        if (hasLeftOnly)
        {
            return ComparisonStatus.LeftOnly;
        }

        if (hasRightOnly)
        {
            return ComparisonStatus.RightOnly;
        }

        return ComparisonStatus.Equal;
    }

    private void PopulateLookup(ComparisonNode node, ImmutableDictionary<string, ComparisonNode>.Builder builder)
    {
        builder[node.RelativePath] = node;
        foreach (var child in node.Children)
        {
            PopulateLookup(child, builder);
        }
    }

    private MutableEntry GetOrCreateEntry(string relativePath, string name, ComparisonNodeType nodeType)
    {
        if (_entries.TryGetValue(relativePath, out var existing))
        {
            if (!string.IsNullOrEmpty(name))
            {
                existing.Name = name;
            }

            existing.NodeType = nodeType;
            return existing;
        }

        var entry = new MutableEntry(name, relativePath, nodeType, _pathComparer);
        _entries[relativePath] = entry;
        return entry;
    }

    private void AttachToParent(MutableEntry entry)
    {
        var parentPath = GetParent(entry.RelativePath);
        if (parentPath is null)
        {
            _rootKey = entry.RelativePath;
            return;
        }

        var parent = GetOrCreateEntry(parentPath, ExtractName(parentPath), ComparisonNodeType.Directory);
        parent.Children.Add(entry.RelativePath);
    }

    private string ExtractName(string relativePath)
    {
        if (_entries.TryGetValue(relativePath, out var entry) && !string.IsNullOrEmpty(entry.Name))
        {
            return entry.Name;
        }

        if (string.IsNullOrEmpty(relativePath))
        {
            return relativePath;
        }

        var index = relativePath.LastIndexOf(Path.DirectorySeparatorChar);
        if (index >= 0)
        {
            return relativePath[(index + 1)..];
        }

        return relativePath;
    }

    private static string Normalize(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return string.Empty;
        }

        return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private string? GetParent(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(relativePath);
        if (string.IsNullOrEmpty(parent))
        {
            return string.Empty;
        }

        return Normalize(parent);
    }

    private sealed class MutableEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MutableEntry"/> class.
        /// </summary>
        /// <param name="name">Entry name.</param>
        /// <param name="relativePath">Path relative to the root.</param>
        /// <param name="nodeType">Node type associated with the entry.</param>
        /// <param name="pathComparer">Comparer used to track child paths.</param>
        public MutableEntry(string name, string relativePath, ComparisonNodeType nodeType, IEqualityComparer<string> pathComparer)
        {
            Name = name;
            RelativePath = relativePath;
            NodeType = nodeType;
            Children = new HashSet<string>(pathComparer);
        }

        /// <summary>
        /// Gets or sets the entry name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the relative path for the entry.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets or sets the node type represented by the entry.
        /// </summary>
        public ComparisonNodeType NodeType { get; set; }

        /// <summary>
        /// Gets or sets the explicit status applied to the entry, if any.
        /// </summary>
        public ComparisonStatus? ExplicitStatus { get; set; }

        /// <summary>
        /// Gets or sets file detail information when applicable.
        /// </summary>
        public FileComparisonDetail? Detail { get; set; }

        /// <summary>
        /// Gets the child entry paths tracked for the node.
        /// </summary>
        public HashSet<string> Children { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the entry has completed processing.
        /// </summary>
        public bool IsCompleted { get; set; }
    }
}

/// <summary>
/// Represents a snapshot of the comparison tree emitted by the update adapter.
/// </summary>
/// <param name="Root">Root node for the comparison tree.</param>
/// <param name="Nodes">Lookup of nodes by their relative path.</param>
public sealed record ComparisonTreeSnapshot(
    ComparisonNode Root,
    ImmutableDictionary<string, ComparisonNode> Nodes
);

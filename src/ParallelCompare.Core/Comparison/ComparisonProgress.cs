using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Channels;

namespace ParallelCompare.Core.Comparison;

public sealed record ComparisonNodeUpdate(ComparisonNode Node, bool IsFinal);

public interface IComparisonProgressSink
{
    void Publish(ComparisonNodeUpdate update);

    void Completed(ComparisonSummary summary);
}

public sealed record ComparisonTreeSnapshot(
    ComparisonNode Root,
    ComparisonSummary? Summary,
    bool IsCompleted);

public sealed class ComparisonTreeStreamAdapter : IComparisonProgressSink
{
    private readonly object _syncRoot = new();
    private readonly ConcurrentDictionary<string, NodeState> _nodes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _childMap = new(StringComparer.Ordinal);
    private readonly Channel<ComparisonTreeSnapshot> _channel = Channel.CreateUnbounded<ComparisonTreeSnapshot>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    private readonly string _defaultRootName;

    private ComparisonSummary? _summary;
    private bool _completed;
    private string? _rootName;
    private ComparisonTreeSnapshot? _latestSnapshot;

    public ComparisonTreeStreamAdapter(string? rootDisplayName = null)
    {
        _defaultRootName = rootDisplayName ?? "root";
    }

    public ChannelReader<ComparisonTreeSnapshot> Updates => _channel.Reader;

    public ComparisonTreeSnapshot Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _latestSnapshot ?? BuildSnapshot();
            }
        }
    }

    public void Publish(ComparisonNodeUpdate update)
    {
        lock (_syncRoot)
        {
            var path = update.Node.RelativePath;
            _nodes[path] = new NodeState(update.Node, update.IsFinal);
            RegisterChild(GetParentRelativePath(path), path);

            if (string.IsNullOrEmpty(path))
            {
                _rootName = update.Node.Name;
            }

            var snapshot = BuildSnapshot();
            _latestSnapshot = snapshot;
            _channel.Writer.TryWrite(snapshot);
        }
    }

    public void Completed(ComparisonSummary summary)
    {
        lock (_syncRoot)
        {
            _summary = summary;
            _completed = true;

            var snapshot = BuildSnapshot();
            _latestSnapshot = snapshot;
            _channel.Writer.TryWrite(snapshot);
            _channel.Writer.TryComplete();
        }
    }

    private void RegisterChild(string? parentPath, string childPath)
    {
        var key = parentPath ?? string.Empty;
        var set = _childMap.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        set[childPath] = 0;
    }

    private ComparisonTreeSnapshot BuildSnapshot()
    {
        var root = BuildNode(string.Empty);
        return new ComparisonTreeSnapshot(root, _summary, _completed);
    }

    private ComparisonNode BuildNode(string relativePath)
    {
        if (!_nodes.TryGetValue(relativePath, out var state))
        {
            var children = BuildChildren(relativePath);
            var name = string.IsNullOrEmpty(relativePath)
                ? _rootName ?? _defaultRootName
                : GetDisplayName(relativePath);
            var status = DetermineStatus(children);

            return new ComparisonNode(
                name,
                relativePath,
                ComparisonNodeType.Directory,
                status,
                null,
                children);
        }

        if (state.Node.NodeType == ComparisonNodeType.File)
        {
            return state.Node;
        }

        var builtChildren = BuildChildren(relativePath);

        if (state.IsFinal)
        {
            return state.Node with { Children = builtChildren };
        }

        var computedStatus = DetermineStatus(builtChildren);
        return state.Node with { Status = computedStatus, Children = builtChildren };
    }

    private ImmutableArray<ComparisonNode> BuildChildren(string? relativePath)
    {
        var key = relativePath ?? string.Empty;
        if (!_childMap.TryGetValue(key, out var children) || children.IsEmpty)
        {
            return ImmutableArray<ComparisonNode>.Empty;
        }

        var ordered = children.Keys.OrderBy(x => x, StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<ComparisonNode>();
        foreach (var childPath in ordered)
        {
            builder.Add(BuildNode(childPath));
        }

        return builder.ToImmutable();
    }

    private static string GetDisplayName(string relativePath)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var index = relativePath.LastIndexOfAny(separators);
        return index >= 0 ? relativePath[(index + 1)..] : relativePath;
    }

    private static string? GetParentRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var index = relativePath.LastIndexOfAny(separators);
        if (index < 0)
        {
            return string.Empty;
        }

        return relativePath[..index];
    }

    private static ComparisonStatus DetermineStatus(IEnumerable<ComparisonNode> children)
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

    private sealed record NodeState(ComparisonNode Node, bool IsFinal);
}

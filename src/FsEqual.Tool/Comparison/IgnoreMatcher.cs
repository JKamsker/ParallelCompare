using DotNet.Globbing;

namespace FsEqual.Tool.Comparison;

internal sealed class IgnoreMatcher
{
    private readonly List<Glob> _globs;

    public IgnoreMatcher(IEnumerable<string> patterns, bool caseSensitive)
    {
        var options = new GlobOptions
        {
            Evaluation = { CaseInsensitive = !caseSensitive }
        };

        _globs = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Glob.Parse(p.Trim(), options))
            .ToList();
    }

    public bool ShouldIgnore(string relativePath, bool isDirectory)
    {
        if (_globs.Count == 0)
        {
            return false;
        }

        var candidate = Normalize(relativePath);
        var directoryCandidate = isDirectory ? candidate.TrimEnd('/') + "/" : candidate;

        foreach (var glob in _globs)
        {
            if (glob.IsMatch(candidate) || glob.IsMatch(directoryCandidate))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
        => value.Replace('\\', '/');
}

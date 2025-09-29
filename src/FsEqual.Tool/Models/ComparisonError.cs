namespace FsEqual.Tool.Models;

public sealed record ComparisonError(string Path, string Message, Exception? Exception = null);

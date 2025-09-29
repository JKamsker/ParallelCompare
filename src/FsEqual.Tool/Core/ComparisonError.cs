namespace FsEqual.Tool.Core;

internal sealed class ComparisonError
{
    public ComparisonError(string scope, string message)
    {
        Scope = scope;
        Message = message;
    }

    public string Scope { get; }

    public string Message { get; }
}

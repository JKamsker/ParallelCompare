namespace FsEqual.Tool.Commands;

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message, int exitCode = 2)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

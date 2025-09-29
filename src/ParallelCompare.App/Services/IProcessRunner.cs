using System.Diagnostics;

namespace ParallelCompare.App.Services;

public interface IProcessRunner
{
    Process? Start(ProcessStartInfo startInfo);
}

internal sealed class DefaultProcessRunner : IProcessRunner
{
    public Process? Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}

using System.Diagnostics;

namespace ParallelCompare.App.Services;

/// <summary>
/// Provides a testable abstraction for starting external processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Starts a new process using the provided <see cref="ProcessStartInfo"/>.
    /// </summary>
    /// <param name="startInfo">Process start configuration.</param>
    /// <returns>The started process, or <c>null</c> when launch fails.</returns>
    Process? Start(ProcessStartInfo startInfo);
}

internal sealed class DefaultProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public Process? Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}

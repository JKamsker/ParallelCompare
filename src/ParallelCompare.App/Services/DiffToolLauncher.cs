using System;
using System.Diagnostics;
using System.IO;

namespace ParallelCompare.App.Services;

public sealed class DiffToolLauncher
{
    public (bool Success, string Message) TryLaunch(string? toolPath, string leftPath, string rightPath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return (false, "No diff tool configured.");
        }

        if (!File.Exists(leftPath) || !File.Exists(rightPath))
        {
            return (false, "Both files must exist to launch the diff tool.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add(leftPath);
            startInfo.ArgumentList.Add(rightPath);

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return (false, $"Failed to launch diff tool '{toolPath}'.");
            }

            return (true, $"Launched diff tool '{toolPath}'.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to launch diff tool '{toolPath}': {ex.Message}");
        }
    }
}

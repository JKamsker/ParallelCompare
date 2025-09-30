using System;
using System.Diagnostics;
using System.IO;

namespace FsEqual.App.Services;

/// <summary>
/// Validates inputs and launches configured external diff tools.
/// </summary>
public sealed class DiffToolLauncher
{
    private readonly Func<string, bool> _fileExists;
    private readonly IProcessRunner _processRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiffToolLauncher"/> class using default dependencies.
    /// </summary>
    public DiffToolLauncher()
        : this(File.Exists, new DefaultProcessRunner())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiffToolLauncher"/> class.
    /// </summary>
    /// <param name="fileExists">Function used to check if a file exists.</param>
    /// <param name="processRunner">Process runner used to start the diff tool.</param>
    public DiffToolLauncher(Func<string, bool> fileExists, IProcessRunner processRunner)
    {
        _fileExists = fileExists;
        _processRunner = processRunner;
    }

    /// <summary>
    /// Attempts to launch the diff tool for the provided files.
    /// </summary>
    /// <param name="toolPath">Path to the diff tool executable.</param>
    /// <param name="leftPath">Left file path.</param>
    /// <param name="rightPath">Right file path.</param>
    /// <returns>A tuple indicating success and an informational message.</returns>
    public (bool Success, string Message) TryLaunch(string? toolPath, string leftPath, string rightPath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return (false, "No diff tool configured.");
        }

        if (!_fileExists(leftPath) || !_fileExists(rightPath))
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

            var process = _processRunner.Start(startInfo);
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

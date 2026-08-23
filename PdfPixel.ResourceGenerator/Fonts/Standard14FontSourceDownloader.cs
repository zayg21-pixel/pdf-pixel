using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PdfPixel.ResourceGenerator.Fonts;

/// <summary>
/// Clones the URW Core 35 font repository the Standard 14 text substitutes are built from.
/// </summary>
internal static class Standard14FontSourceDownloader
{
    private const string GitExecutable = "git";

    /// <summary>
    /// Clones <paramref name="repositoryUrl"/> into <paramref name="workDirectory"/>, replacing any
    /// clone already there. Returns the path to the working tree.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git could not be started, or the clone failed.</exception>
    public static async Task<string> CloneAsync(string repositoryUrl, string workDirectory)
    {
        DeleteClone(workDirectory);

        Console.WriteLine($"Cloning {repositoryUrl} ...");

        ProcessStartInfo startInfo = new(GitExecutable)
        {
            UseShellExecute = false,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--depth");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add(repositoryUrl);
        startInfo.ArgumentList.Add(workDirectory);

        using Process? process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"'{GitExecutable}' could not be started.");
        }

        string error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Cloning '{repositoryUrl}' failed with exit code {process.ExitCode}: {error}");
        }

        return workDirectory;
    }

    /// <summary>
    /// Deletes a clone left by an earlier run, clearing the read-only attribute git sets on the files
    /// in its object store first.
    /// </summary>
    private static void DeleteClone(string workDirectory)
    {
        if (!Directory.Exists(workDirectory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(workDirectory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(workDirectory, recursive: true);
    }
}

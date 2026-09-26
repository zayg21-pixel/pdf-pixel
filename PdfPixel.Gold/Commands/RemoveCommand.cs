using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Utilities;

namespace PdfPixel.Gold.Commands;

/// <summary>
/// Unregisters a single PDF, and deletes the downloaded file and the golden snapshots that went
/// with it.
/// </summary>
internal static class RemoveCommand
{
    /// <summary>
    /// Removes the PDF named by the arguments and returns the process exit code: 0 on success.
    /// </summary>
    public static int Run(GoldPaths paths, GoldManifest manifest, string[] arguments)
    {
        if (arguments.Length != 1)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, "Usage: remove <pdf name>");

            return 1;
        }

        string fileName = FileUtilities.GetPdfFileName(arguments[0]);
        string pdfPath = paths.GetSourcePath(fileName);
        string snapshotDirectory = paths.GetSnapshotDirectory(fileName);

        int registrationCount = manifest.Files.RemoveAll(existing => string.Equals(existing.Name, fileName, StringComparison.OrdinalIgnoreCase));
        bool sourceExists = File.Exists(pdfPath);
        bool snapshotsExist = Directory.Exists(snapshotDirectory);

        // Nothing of this PDF is in the corpus, so the name is a typo rather than a member to drop.
        if (registrationCount == 0 && !sourceExists && !snapshotsExist)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, $"{fileName} is not registered in {paths.ManifestPath}, and has neither a source file nor a snapshot.");

            return 1;
        }

        if (registrationCount > 0)
        {
            manifest.Save(paths.ManifestPath);
        }

        if (sourceExists)
        {
            File.Delete(pdfPath);
        }

        if (snapshotsExist)
        {
            Directory.Delete(snapshotDirectory, recursive: true);
        }

        string registration = registrationCount > 0 ? "unregistered" : "was not registered";
        string source = sourceExists ? "source deleted" : "no source";
        string snapshots = snapshotsExist ? "snapshots deleted" : "no snapshots";
        ConsoleUtilities.Write(ConsoleColor.Green, $"{fileName,-60} {registration}, {source}, {snapshots}");

        return 0;
    }
}

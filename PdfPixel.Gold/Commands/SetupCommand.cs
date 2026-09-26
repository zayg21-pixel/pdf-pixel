using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Utilities;

namespace PdfPixel.Gold.Commands;

/// <summary>
/// Downloads every PDF of the manifest that is not in the source folder yet. A file already there
/// is never downloaded again, and a download is only kept when its SHA-256 matches the manifest.
/// </summary>
internal static class SetupCommand
{
    /// <summary>
    /// Downloads the missing PDFs and returns the process exit code: 0 when none failed.
    /// </summary>
    public static async Task<int> RunAsync(GoldPaths paths, GoldManifest manifest)
    {
        Directory.CreateDirectory(paths.SourceDirectory);

        int present = 0;
        int downloaded = 0;
        int failed = 0;

        foreach (GoldEntry entry in manifest.Files)
        {
            string pdfPath = paths.GetSourcePath(entry.Name);

            if (File.Exists(pdfPath))
            {
                present++;
                continue;
            }

            byte[] content;
            try
            {
                content = await DownloadUtilities.Client.GetByteArrayAsync(entry.Url);
            }
            catch (HttpRequestException exception)
            {
                ConsoleUtilities.Write(ConsoleColor.Red, $"{entry.Name,-60} download failed: {exception.Message} ({entry.Url})");
                failed++;
                continue;
            }

            string sha256 = FileUtilities.ComputeSha256(content);
            if (sha256 != entry.Sha256)
            {
                ConsoleUtilities.Write(ConsoleColor.Red, $"{entry.Name,-60} sha256 mismatch: got {sha256} ({entry.Url})");
                failed++;
                continue;
            }

            await File.WriteAllBytesAsync(pdfPath, content);
            ConsoleUtilities.Write(ConsoleColor.Green, $"{entry.Name,-60} downloaded");
            downloaded++;
        }

        ConsoleColor summaryColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
        ConsoleUtilities.Write(summaryColor, $"{present} present, {downloaded} downloaded, {failed} failed, of {manifest.Files.Count} PDF(s).");

        return failed == 0 ? 0 : 1;
    }
}

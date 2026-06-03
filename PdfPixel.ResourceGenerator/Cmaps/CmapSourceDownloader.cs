using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace PdfPixel.ResourceGenerator.Cmaps;

/// <summary>
/// Downloads and extracts the Adobe CMap resources archive.
/// </summary>
internal static class CmapSourceDownloader
{
    /// <summary>
    /// Downloads the archive at <paramref name="sourceUrl"/> and extracts it into <paramref name="workDirectory"/>.
    /// Returns the path to the extracted root folder.
    /// </summary>
    public static async Task<string> DownloadAndExtractAsync(string sourceUrl, string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);

        string zipPath = Path.Combine(workDirectory, "cmap-resources.zip");

        Console.WriteLine($"Downloading {sourceUrl} ...");
        using (HttpClient httpClient = new())
        {
            byte[] zipBytes = await httpClient.GetByteArrayAsync(new Uri(sourceUrl)).ConfigureAwait(false);
            await File.WriteAllBytesAsync(zipPath, zipBytes).ConfigureAwait(false);
        }

        string extractPath = Path.Combine(workDirectory, "cmap-resources");
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        Console.WriteLine($"Extracting to {extractPath} ...");
        ZipFile.ExtractToDirectory(zipPath, extractPath);

        string[] topLevel = Directory.GetDirectories(extractPath);
        if (topLevel.Length == 1)
        {
            return topLevel[0];
        }

        return extractPath;
    }
}

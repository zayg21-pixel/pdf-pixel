using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace PdfPixel.ResourceGenerator.Fonts;

/// <summary>
/// Downloads and extracts the Croscore font archive.
/// </summary>
internal static class Standard14FontSourceDownloader
{
    /// <summary>
    /// Downloads the gzipped tar archive at <paramref name="sourceUrl"/> and extracts it into
    /// <paramref name="workDirectory"/>. Returns the path to the extracted root folder.
    /// </summary>
    public static async Task<string> DownloadCroscoreAsync(string sourceUrl, string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);

        string archivePath = Path.Combine(workDirectory, "croscorefonts.tar.gz");

        Console.WriteLine($"Downloading {sourceUrl} ...");
        using (HttpClient httpClient = new())
        {
            byte[] archiveBytes = await httpClient.GetByteArrayAsync(new Uri(sourceUrl)).ConfigureAwait(false);
            await File.WriteAllBytesAsync(archivePath, archiveBytes).ConfigureAwait(false);
        }

        string extractPath = Path.Combine(workDirectory, "croscorefonts");
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        Directory.CreateDirectory(extractPath);

        Console.WriteLine($"Extracting to {extractPath} ...");
        using (FileStream archiveStream = File.OpenRead(archivePath))
        {
            using GZipStream decompressed = new(archiveStream, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(decompressed, extractPath, overwriteFiles: true).ConfigureAwait(false);
        }

        string[] topLevel = Directory.GetDirectories(extractPath);
        if (topLevel.Length == 1)
        {
            return topLevel[0];
        }

        return extractPath;
    }
}

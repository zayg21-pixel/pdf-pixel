using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Utilities;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.Gold.Commands;

/// <summary>
/// Registers a single PDF from its URL: downloads it, records its SHA-256, times one render to decide
/// whether it is a full-run member, and writes goldens for the pages that have none yet.
/// </summary>
internal static class AddCommand
{
    // A PDF that renders slower than this is registered as a full-run member, so that a short run
    // stays quick enough to be used after every fix.
    public const double FullRunThresholdSeconds = 1.0;

    /// <summary>
    /// Registers the PDF described by the arguments and returns the process exit code: 0 on success.
    /// </summary>
    public static async Task<int> RunAsync(GoldPaths paths, PdfDocumentReader reader, GoldManifest manifest, string[] arguments)
    {
        string? password = null;
        string? origin = null;
        List<string> positional = new();

        for (int index = 0; index < arguments.Length; index++)
        {
            bool isPassword = string.Equals(arguments[index], "--password", StringComparison.OrdinalIgnoreCase);
            bool isOrigin = string.Equals(arguments[index], "--origin", StringComparison.OrdinalIgnoreCase);

            if (!isPassword && !isOrigin)
            {
                positional.Add(arguments[index]);

                continue;
            }

            if (index + 1 == arguments.Length)
            {
                ConsoleUtilities.Write(ConsoleColor.Red, $"{arguments[index]} needs a value.");

                return 1;
            }

            if (isPassword)
            {
                password = arguments[index + 1];
            }
            else
            {
                origin = arguments[index + 1];
            }

            index++;
        }

        if (positional.Count < 2 || positional.Count > 3)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, "Usage: add <pdf name> <url> [comma separated page numbers] [--password <user password>] [--origin <url>]");

            return 1;
        }

        string fileName = FileUtilities.GetPdfFileName(positional[0]);
        string url = positional[1];
        List<int> pages = new();

        if (positional.Count == 3)
        {
            foreach (string part in positional[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, out int pageNumber) || pageNumber < 1)
                {
                    ConsoleUtilities.Write(ConsoleColor.Red, $"'{part}' is not a page number; pass 1-based page numbers as 1,2,3.");

                    return 1;
                }

                if (!pages.Contains(pageNumber))
                {
                    pages.Add(pageNumber);
                }
            }

            pages.Sort();
        }

        byte[] content;
        try
        {
            content = await DownloadUtilities.Client.GetByteArrayAsync(url);
        }
        catch (HttpRequestException exception)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, $"{fileName,-60} download failed: {exception.Message} ({url})");

            return 1;
        }

        string pdfPath = paths.GetSourcePath(fileName);
        Directory.CreateDirectory(paths.SourceDirectory);
        await File.WriteAllBytesAsync(pdfPath, content);

        string snapshotDirectory = paths.GetSnapshotDirectory(fileName);

        // The registration is timed to classify the PDF, so the clock covers the rendering only and
        // is held while a golden is written — a run never pays that cost.
        Stopwatch stopwatch = new();
        int pageCount = 0;
        int writtenCount = 0;

        try
        {
            stopwatch.Start();

            foreach ((int PageNumber, SKBitmap Bitmap) renderedPage in PdfUtilities.RenderPages(reader, pdfPath, pages, password))
            {
                stopwatch.Stop();

                using SKBitmap bitmap = renderedPage.Bitmap;
                string goldenPath = Path.Combine(snapshotDirectory, $"{renderedPage.PageNumber}_source.png");
                pageCount++;

                // An existing golden is the baseline a comparison is judged against and is never
                // replaced here; only the pages that have no golden yet get one.
                if (!File.Exists(goldenPath))
                {
                    Directory.CreateDirectory(snapshotDirectory);
                    ImageUtilities.SavePng(bitmap, goldenPath);
                    writtenCount++;
                }

                stopwatch.Start();
            }

            stopwatch.Stop();
        }
        catch (Exception exception)
        {
            ConsoleUtilities.Write(ConsoleColor.Magenta, $"{fileName,-60} FAILED {exception.Message}");

            return 1;
        }

        // A document that renders nothing would be registered with no golden at all, and every later
        // comparison of it would pass without having looked at anything.
        if (pageCount == 0)
        {
            ConsoleUtilities.Write(ConsoleColor.Magenta, $"{fileName,-60} FAILED the document rendered no page, so there is nothing to compare against.");

            return 1;
        }

        bool fullRunOnly = stopwatch.Elapsed.TotalSeconds > FullRunThresholdSeconds;

        GoldEntry entry = new()
        {
            Name = fileName,
            Url = url,
            Sha256 = FileUtilities.ComputeSha256(content),
            Origin = origin,
            Pages = pages,
            FullRun = fullRunOnly ? true : null,
            Password = password,
        };

        manifest.Files.RemoveAll(existing => string.Equals(existing.Name, fileName, StringComparison.OrdinalIgnoreCase));
        manifest.Files.Add(entry);
        manifest.Save(paths.ManifestPath);

        string pageScope = pages.Count == 0 ? "all pages" : $"page(s) {string.Join(", ", pages)}";
        ConsoleUtilities.Write(
            ConsoleColor.Green,
            $"{fileName,-60} {pageScope}, {stopwatch.Elapsed.TotalSeconds:F2} s, {(fullRunOnly ? "full run only" : "short + full run")}, {writtenCount} golden(s) written");

        return 0;
    }
}

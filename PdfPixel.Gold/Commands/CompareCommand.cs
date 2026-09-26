using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Utilities;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.Gold.Commands;

/// <summary>
/// Renders the selected PDFs and compares every page against its golden snapshot. A differing page
/// is written next to its golden as "{page number}_result.png".
/// </summary>
internal static class CompareCommand
{
    /// <summary>
    /// Compares every entry and returns the process exit code: 0 when all of them match.
    /// </summary>
    public static int Run(GoldPaths paths, PdfDocumentReader reader, List<GoldEntry> entries, RunOptions options)
    {
        // Every differing page found by this run is collected flat into "Inspect", starting from a
        // clean slate, so a previous run's leftovers never get mixed in with this one's.
        if (options.Inspect)
        {
            if (Directory.Exists(paths.InspectDirectory))
            {
                Directory.Delete(paths.InspectDirectory, recursive: true);
            }

            Directory.CreateDirectory(paths.InspectDirectory);
        }

        Stopwatch runStopwatch = Stopwatch.StartNew();
        int differentCount = 0;
        int failedCount = 0;

        foreach (GoldEntry entry in entries)
        {
            try
            {
                if (!Compare(paths, reader, entry, options.Inspect))
                {
                    differentCount++;
                }
            }
            catch (Exception exception)
            {
                failedCount++;
                ConsoleUtilities.Write(ConsoleColor.Magenta, $"{entry.Name,-60} FAILED {exception.Message}");
            }

            PdfUtilities.PurgeSkiaCaches();
        }

        runStopwatch.Stop();

        int problemCount = differentCount + failedCount;
        string summary = $"{entries.Count - problemCount} of {entries.Count} PDF(s) match in the {options.Scope} run in {runStopwatch.Elapsed.TotalSeconds:F1} s; {differentCount} differ, {failedCount} failed.";
        ConsoleUtilities.Write(problemCount == 0 ? ConsoleColor.Green : ConsoleColor.Red, summary);

        return problemCount == 0 ? 0 : 1;
    }

    private static bool Compare(GoldPaths paths, PdfDocumentReader reader, GoldEntry entry, bool inspect)
    {
        string pdfPath = paths.GetSourcePath(entry.Name);
        string snapshotDirectory = paths.GetSnapshotDirectory(entry.Name);
        Directory.CreateDirectory(snapshotDirectory);

        bool matched = true;

        foreach ((int PageNumber, SKBitmap Bitmap) renderedPage in PdfUtilities.RenderPages(reader, pdfPath, entry.Pages, entry.Password))
        {
            using SKBitmap page = renderedPage.Bitmap;
            int pageNumber = renderedPage.PageNumber;
            string sourcePath = Path.Combine(snapshotDirectory, $"{pageNumber}_source.png");
            string resultPath = Path.Combine(snapshotDirectory, $"{pageNumber}_result.png");

            if (!File.Exists(sourcePath))
            {
                ImageUtilities.SavePng(page, resultPath);
                ConsoleUtilities.Write(ConsoleColor.Yellow, $"{entry.Name,-60} page {pageNumber} NO GOLDEN SNAPSHOT");
                matched = false;

                continue;
            }

            using SKBitmap golden = ImageUtilities.LoadPng(sourcePath);

            if (golden.Width != page.Width || golden.Height != page.Height)
            {
                ImageUtilities.SavePng(page, resultPath);
                ConsoleUtilities.Write(ConsoleColor.Red, $"{entry.Name,-60} page {pageNumber} SIZE {page.Width}x{page.Height}, golden {golden.Width}x{golden.Height}");
                matched = false;

                continue;
            }

            int differentPixels = ImageUtilities.CountDifferentPixels(golden, page);

            if (differentPixels == 0)
            {
                File.Delete(resultPath);
                ConsoleUtilities.Write(ConsoleColor.Green, $"{entry.Name,-60} page {pageNumber} OK");

                continue;
            }

            ImageUtilities.SavePng(page, resultPath);

            double differentPercentage = differentPixels * 100.0 / ((long)page.Width * page.Height);
            ConsoleUtilities.Write(ConsoleColor.Red, $"{entry.Name,-60} page {pageNumber} DIFF {differentPixels} pixel(s), {differentPercentage:F2}%");
            matched = false;

            if (inspect)
            {
                CopyToInspect(paths.InspectDirectory, pdfPath, entry.Name, pageNumber, sourcePath, resultPath, golden, page);
            }
        }

        return matched;
    }

    // Gathers everything needed to eyeball one differing page - the source PDF and its golden,
    // result, and diff images - flat into the inspect folder, named by PDF and page so files from
    // different PDFs never collide.
    private static void CopyToInspect(string inspectDirectory, string pdfPath, string pdfName, int pageNumber, string sourcePath, string resultPath, SKBitmap golden, SKBitmap rendered)
    {
        string stem = Path.GetFileNameWithoutExtension(pdfName);

        File.Copy(pdfPath, Path.Combine(inspectDirectory, pdfName), overwrite: true);
        File.Copy(sourcePath, Path.Combine(inspectDirectory, $"{stem}_{pageNumber}_source.png"), overwrite: true);
        File.Copy(resultPath, Path.Combine(inspectDirectory, $"{stem}_{pageNumber}_result.png"), overwrite: true);

        using SKBitmap diff = ImageUtilities.CreateDiffImage(golden, rendered);
        ImageUtilities.SavePng(diff, Path.Combine(inspectDirectory, $"{stem}_{pageNumber}_diff.png"));
    }
}

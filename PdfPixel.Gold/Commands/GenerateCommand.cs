using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Utilities;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.Gold.Commands;

/// <summary>
/// Renders the selected PDFs and rewrites their golden snapshots.
/// </summary>
internal static class GenerateCommand
{
    /// <summary>
    /// Rewrites the goldens of every entry and returns the process exit code: 0 when none failed.
    /// </summary>
    public static int Run(GoldPaths paths, PdfDocumentReader reader, List<GoldEntry> entries, RunOptions options)
    {
        Stopwatch runStopwatch = Stopwatch.StartNew();
        int failedCount = 0;

        foreach (GoldEntry entry in entries)
        {
            try
            {
                Generate(paths, reader, entry);
            }
            catch (Exception exception)
            {
                failedCount++;
                ConsoleUtilities.Write(ConsoleColor.Magenta, $"{entry.Name,-60} FAILED {exception.Message}");
            }

            PdfUtilities.PurgeSkiaCaches();
        }

        runStopwatch.Stop();

        string summary = $"Generated {options.Scope} snapshots for {entries.Count - failedCount} of {entries.Count} PDF(s) in {runStopwatch.Elapsed.TotalSeconds:F1} s; {failedCount} failed.";
        ConsoleUtilities.Write(failedCount == 0 ? ConsoleColor.Green : ConsoleColor.Red, summary);

        return failedCount == 0 ? 0 : 1;
    }

    private static void Generate(GoldPaths paths, PdfDocumentReader reader, GoldEntry entry)
    {
        string snapshotDirectory = paths.GetSnapshotDirectory(entry.Name);
        Directory.CreateDirectory(snapshotDirectory);

        int pageCount = 0;

        foreach ((int PageNumber, SKBitmap Bitmap) renderedPage in PdfUtilities.RenderPages(reader, paths.GetSourcePath(entry.Name), entry.Pages, entry.Password))
        {
            using SKBitmap bitmap = renderedPage.Bitmap;

            ImageUtilities.SavePng(bitmap, Path.Combine(snapshotDirectory, $"{renderedPage.PageNumber}_source.png"));

            // The previous run's comparison output describes the snapshot that was just replaced.
            File.Delete(Path.Combine(snapshotDirectory, $"{renderedPage.PageNumber}_result.png"));
            pageCount++;
        }

        ConsoleUtilities.Write(ConsoleColor.Green, $"{entry.Name,-60} {pageCount} page(s)");
    }
}

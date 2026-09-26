using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Utilities;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.Gold.Commands;

/// <summary>
/// Renders the selected PDFs to measure them only: nothing is compared against a golden snapshot
/// and no file is written. The run closes with the pages that crossed the outlier thresholds.
/// </summary>
internal static class AnalyzeCommand
{
    private const double PageTimeOutlierSeconds = 1.0;

    private const long PageMemoryOutlierBytes = 50L * 1024 * 1024;

    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    /// <summary>
    /// Analyzes every entry and returns the process exit code: 0 when none failed.
    /// </summary>
    public static int Run(GoldPaths paths, PdfDocumentReader reader, List<GoldEntry> entries, RunOptions options)
    {
        if (options.MeasureMemory)
        {
            MemoryUtilities.StartPeakSampler();
        }

        Stopwatch runStopwatch = Stopwatch.StartNew();
        int failedCount = 0;
        List<PageStatistics> timeOutliers = new();
        List<PageStatistics> memoryOutliers = new();

        foreach (GoldEntry entry in entries)
        {
            try
            {
                Analyze(paths, reader, entry, options.MeasureMemory, timeOutliers, memoryOutliers);
            }
            catch (Exception exception)
            {
                failedCount++;
                ConsoleUtilities.Write(ConsoleColor.Magenta, $"{entry.Name,-60} FAILED {exception.Message}");
            }

            PdfUtilities.PurgeSkiaCaches();
        }

        runStopwatch.Stop();

        string summary = $"Analyzed {entries.Count - failedCount} of {entries.Count} PDF(s) in the {options.Scope} run in {runStopwatch.Elapsed.TotalSeconds:F1} s; {failedCount} failed.";
        ConsoleUtilities.Write(failedCount == 0 ? ConsoleColor.Green : ConsoleColor.Red, summary);

        PrintOutliers(timeOutliers, memoryOutliers, options.MeasureMemory);

        return failedCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// Renders one PDF to measure it only: nothing is compared against a golden snapshot and no file
    /// is written. Every page reports the time it took and, when <paramref name="measureMemory"/> is
    /// set, the peak memory it reached while it rendered along with the memory the document held on
    /// to afterwards; the document reports its totals, and the pages that go over the outlier
    /// thresholds are collected into <paramref name="timeOutliers"/> and <paramref name="memoryOutliers"/>.
    /// </summary>
    private static void Analyze(
        GoldPaths paths,
        PdfDocumentReader reader,
        GoldEntry entry,
        bool measureMemory,
        List<PageStatistics> timeOutliers,
        List<PageStatistics> memoryOutliers)
    {
        string pdfName = entry.Name;
        Stopwatch stopwatch = new();
        int pageCount = 0;
        double documentSeconds = 0;
        long documentPeakBytes = 0;
        long documentPeakManagedBytes = 0;
        long documentRetainedBytes = 0;
        long heldManagedBytes = 0;

        // The pages are pulled one at a time, so the clock and the memory reading cover the render of
        // a single page; the first page carries the cost of opening the document with it.
        using IEnumerator<(int PageNumber, SKBitmap Bitmap)> renderedPages = PdfUtilities.RenderPages(reader, paths.GetSourcePath(entry.Name), entry.Pages, entry.Password).GetEnumerator();

        while (true)
        {
            long managedBefore = 0;
            long privateBefore = 0;

            if (measureMemory)
            {
                managedBefore = GC.GetTotalMemory(forceFullCollection: true);

                // The watermarks start at what the process already holds, so the page is charged for
                // what it adds rather than for everything the run has committed so far.
                privateBefore = MemoryUtilities.GetPrivateMemoryBytes();
                MemoryUtilities.ResetPeaks(privateBefore, managedBefore);
            }

            stopwatch.Restart();
            bool hasPage = renderedPages.MoveNext();
            stopwatch.Stop();

            // Read while nothing has been released yet, so the watermarks still cover the render.
            long peakPrivateBytes = MemoryUtilities.PeakPrivateBytes;
            long peakManagedBytes = MemoryUtilities.PeakManagedBytes;

            if (!hasPage)
            {
                break;
            }

            (int PageNumber, SKBitmap Bitmap) renderedPage = renderedPages.Current;

            // The bitmap is this page's output rather than something the document holds on to, so it
            // is released before the memory is read back.
            renderedPage.Bitmap.Dispose();

            PageMemory? pageMemory = null;

            if (measureMemory)
            {
                heldManagedBytes = GC.GetTotalMemory(forceFullCollection: true);

                pageMemory = new(peakPrivateBytes - privateBefore, peakManagedBytes - managedBefore, heldManagedBytes - managedBefore);
                documentPeakBytes = Math.Max(documentPeakBytes, pageMemory.PeakBytes);
                documentPeakManagedBytes = Math.Max(documentPeakManagedBytes, pageMemory.PeakManagedBytes);
                documentRetainedBytes += pageMemory.RetainedBytes;
            }

            double seconds = stopwatch.Elapsed.TotalSeconds;
            PageStatistics statistics = new(pdfName, renderedPage.PageNumber, seconds, pageMemory);

            pageCount++;
            documentSeconds += seconds;

            bool slow = seconds > PageTimeOutlierSeconds;
            bool heavy = pageMemory != null && pageMemory.PeakBytes > PageMemoryOutlierBytes;

            if (slow)
            {
                timeOutliers.Add(statistics);
            }

            if (heavy)
            {
                memoryOutliers.Add(statistics);
            }

            ConsoleUtilities.Write(slow || heavy ? ConsoleColor.Red : ConsoleColor.Gray, FormatStatistics(statistics));
        }

        string total = $"{pdfName,-60} TOTAL {pageCount,-4} page(s) {documentSeconds,6:F3} s";

        if (measureMemory)
        {
            total += $" peak {documentPeakBytes / BytesPerMegabyte,9:F1} MB"
                + $" managed {documentPeakManagedBytes / BytesPerMegabyte,9:F1} MB"
                + $" | retained {documentRetainedBytes / BytesPerMegabyte,8:F1} MB"
                + $" | held heap {heldManagedBytes / BytesPerMegabyte,8:F1} MB";
        }

        ConsoleUtilities.Write(ConsoleColor.Cyan, total);
    }

    /// <summary>
    /// Prints the pages an analysis run found to be outliers, the slow ones first and the memory
    /// hungry ones after them, each group ordered by what made it an outlier.
    /// </summary>
    private static void PrintOutliers(List<PageStatistics> timeOutliers, List<PageStatistics> memoryOutliers, bool measureMemory)
    {
        timeOutliers.Sort(static (left, right) => right.Seconds.CompareTo(left.Seconds));

        Console.WriteLine();
        ConsoleUtilities.Write(ConsoleColor.Cyan, $"{timeOutliers.Count} slow page(s):");

        foreach (PageStatistics statistics in timeOutliers)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, FormatStatistics(statistics));
        }

        if (!measureMemory)
        {
            return;
        }

        memoryOutliers.Sort(static (left, right) => PeakMemoryBytes(right).CompareTo(PeakMemoryBytes(left)));

        Console.WriteLine();
        ConsoleUtilities.Write(ConsoleColor.Cyan, $"{memoryOutliers.Count} memory hungry page(s):");

        foreach (PageStatistics statistics in memoryOutliers)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, FormatStatistics(statistics));
        }

        static long PeakMemoryBytes(PageStatistics statistics)
        {
            if (statistics.Memory == null)
            {
                return 0;
            }

            return statistics.Memory.PeakBytes;
        }
    }

    private static string FormatStatistics(PageStatistics statistics)
    {
        string line = $"{statistics.PdfName,-60} page {statistics.PageNumber,-4} {statistics.Seconds,8:F3} s";

        if (statistics.Memory != null)
        {
            line += $" peak {statistics.Memory.PeakBytes / BytesPerMegabyte,9:F1} MB"
                + $" managed {statistics.Memory.PeakManagedBytes / BytesPerMegabyte,9:F1} MB"
                + $" | retained {statistics.Memory.RetainedBytes / BytesPerMegabyte,8:F1} MB";
        }

        return line;
    }

    /// <summary>
    /// What one page of an analysis run cost: the time it took to render, and the memory it grew by
    /// while it did when the run was asked to measure memory.
    /// </summary>
    private sealed record PageStatistics(string PdfName, int PageNumber, double Seconds, PageMemory? Memory);

    /// <summary>
    /// The memory one page took: the highest the whole process and its managed heap reached while it
    /// rendered, and what the document was still holding once the page bitmap had been released. The
    /// difference between the two peaks is what was allocated outside the managed heap.
    /// </summary>
    private sealed record PageMemory(long PeakBytes, long PeakManagedBytes, long RetainedBytes);
}

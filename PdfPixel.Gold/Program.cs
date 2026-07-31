using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Fonts.Management;
using SkiaSharp;
using System.Diagnostics;
using System.IO.Enumeration;

namespace PdfPixel.Gold;

/// <summary>
/// Renders the PDFs in "source" next to this executable and keeps a golden PNG of every page in
/// "snapshots\{pdf name}\{page number}_source.png". A comparison writes what it rendered next to
/// the golden page as "{page number}_result.png", but only when the two differ.
/// </summary>
internal sealed class Program
{
    private static int Main(string[] args)
    {
        EnsureReleaseBuild();

        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            Console.WriteLine("Usage: PdfPixel.Gold [generate|compare] [pdf name or wildcard]...");
            Console.WriteLine("  <no arguments>   compares every PDF in the source folder against its golden snapshot");
            Console.WriteLine("  a* b             compares the selected PDFs only");
            Console.WriteLine("  generate         writes a golden snapshot of every page of every PDF in the source folder");
            Console.WriteLine("  generate a* b    writes golden snapshots for the selected PDFs only");

            return 0;
        }

        // A run compares unless it is asked to generate; every argument that is not the mode selects
        // the PDFs to process.
        bool generate = args.Length > 0 && args[0] == "generate";
        bool hasMode = generate || (args.Length > 0 && args[0] == "compare");

        string sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "source");
        string snapshotsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snapshots");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(snapshotsDirectory);

        List<string> pdfPaths = GetPdfFiles(sourceDirectory, hasMode ? args.AsSpan(1).ToArray() : args);

        if (pdfPaths.Count == 0)
        {
            Write(ConsoleColor.Red, $"No PDF to process in {sourceDirectory}.");

            return 1;
        }

        // A font provider is used to substitute system fonts for fonts not embedded in the PDF, and
        // PdfDocumentReader is the entry point for parsing PDF files.
        IFontProvider fontProvider = new WindowsFontProvider(NullLoggerFactory.Instance);
        PdfDocumentReader reader = new(NullLoggerFactory.Instance, fontProvider);

        Stopwatch runStopwatch = Stopwatch.StartNew();
        int differentCount = 0;
        int failedCount = 0;

        foreach (string pdfPath in pdfPaths)
        {
            string snapshotDirectory = Path.Combine(snapshotsDirectory, Path.GetFileNameWithoutExtension(pdfPath));
            Directory.CreateDirectory(snapshotDirectory);

            try
            {
                if (generate)
                {
                    Generate(reader, pdfPath, snapshotDirectory);
                }
                else if (!Compare(reader, pdfPath, snapshotDirectory))
                {
                    differentCount++;
                }
            }
            catch (Exception exception)
            {
                failedCount++;
                Write(ConsoleColor.Magenta, $"{Path.GetFileName(pdfPath),-60} FAILED {exception.Message}");
            }
        }

        runStopwatch.Stop();

        int problemCount = differentCount + failedCount;
        string summary = generate
            ? $"Generated snapshots for {pdfPaths.Count - failedCount} of {pdfPaths.Count} PDF(s) in {runStopwatch.Elapsed.TotalSeconds:F1} s; {failedCount} failed."
            : $"{pdfPaths.Count - problemCount} of {pdfPaths.Count} PDF(s) match in {runStopwatch.Elapsed.TotalSeconds:F1} s; {differentCount} differ, {failedCount} failed.";

        Write(problemCount == 0 ? ConsoleColor.Green : ConsoleColor.Red, summary);

        return problemCount == 0 ? 0 : 1;
    }

    private static void Generate(PdfDocumentReader reader, string pdfPath, string snapshotDirectory)
    {
        List<SKBitmap> pages = PageRenderer.RenderPages(reader, pdfPath);

        for (int index = 0; index < pages.Count; index++)
        {
            using SKBitmap page = pages[index];
            int pageNumber = index + 1;

            PageRenderer.SavePng(page, Path.Combine(snapshotDirectory, $"{pageNumber}_source.png"));

            // The previous run's comparison output describes the snapshot that was just replaced.
            File.Delete(Path.Combine(snapshotDirectory, $"{pageNumber}_result.png"));
        }

        Write(ConsoleColor.Green, $"{Path.GetFileName(pdfPath),-60} {pages.Count} page(s)");
    }

    private static bool Compare(PdfDocumentReader reader, string pdfPath, string snapshotDirectory)
    {
        List<SKBitmap> pages = PageRenderer.RenderPages(reader, pdfPath);
        string pdfName = Path.GetFileName(pdfPath);
        bool matched = true;

        for (int index = 0; index < pages.Count; index++)
        {
            using SKBitmap page = pages[index];
            int pageNumber = index + 1;
            string sourcePath = Path.Combine(snapshotDirectory, $"{pageNumber}_source.png");
            string resultPath = Path.Combine(snapshotDirectory, $"{pageNumber}_result.png");

            if (!File.Exists(sourcePath))
            {
                PageRenderer.SavePng(page, resultPath);
                Write(ConsoleColor.Yellow, $"{pdfName,-60} page {pageNumber} NO GOLDEN SNAPSHOT");
                matched = false;

                continue;
            }

            using SKBitmap golden = PageRenderer.LoadPng(sourcePath);

            if (golden.Width != page.Width || golden.Height != page.Height)
            {
                PageRenderer.SavePng(page, resultPath);
                Write(ConsoleColor.Red, $"{pdfName,-60} page {pageNumber} SIZE {page.Width}x{page.Height}, golden {golden.Width}x{golden.Height}");
                matched = false;

                continue;
            }

            int differentPixels = PageRenderer.CountDifferentPixels(golden, page);

            if (differentPixels == 0)
            {
                File.Delete(resultPath);
                Write(ConsoleColor.Green, $"{pdfName,-60} page {pageNumber} OK");

                continue;
            }

            PageRenderer.SavePng(page, resultPath);

            double differentPercentage = differentPixels * 100.0 / ((long)page.Width * page.Height);
            Write(ConsoleColor.Red, $"{pdfName,-60} page {pageNumber} DIFF {differentPixels} pixel(s), {differentPercentage:F2}%");
            matched = false;
        }

        return matched;
    }

    // An empty filter list selects every PDF; otherwise a PDF is selected when a filter matches its
    // file name with or without the extension, so both "basicapi" and "bug1*.pdf" work.
    private static List<string> GetPdfFiles(string sourceDirectory, string[] filters)
    {
        List<string> pdfPaths = new();

        foreach (string pdfPath in Directory.EnumerateFiles(sourceDirectory, "*.pdf"))
        {
            if (filters.Length == 0 || Matches(pdfPath, filters))
            {
                pdfPaths.Add(pdfPath);
            }
        }

        pdfPaths.Sort(StringComparer.OrdinalIgnoreCase);

        return pdfPaths;
    }

    private static bool Matches(string pdfPath, string[] filters)
    {
        string fileName = Path.GetFileName(pdfPath);
        string name = Path.GetFileNameWithoutExtension(pdfPath);

        foreach (string filter in filters)
        {
            if (FileSystemName.MatchesSimpleExpression(filter, fileName, ignoreCase: true)
                || FileSystemName.MatchesSimpleExpression(filter, name, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    // Snapshots taken from a debug build are not comparable with the ones taken from a release build.
    private static void EnsureReleaseBuild()
    {
#if DEBUG
        throw new InvalidOperationException("PdfPixel.Gold runs in Release only. Build the release configuration and run it from bin\\Release.");
#endif
    }

    private static void Write(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}

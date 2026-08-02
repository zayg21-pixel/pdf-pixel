using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Fonts.Management;
using SkiaSharp;
using System.Diagnostics;
using System.IO.Enumeration;

namespace PdfPixel.Gold;

/// <summary>
/// Renders the PDFs registered in "gold.json" next to this executable and keeps a golden PNG of
/// every page in "snapshots\{pdf name}\{page number}_source.png". A comparison writes what it
/// rendered next to the golden page as "{page number}_result.png", but only when the two differ.
/// </summary>
internal sealed class Program
{
    // A PDF that renders slower than this is registered as a full-run member, so that a short run
    // stays quick enough to be used after every fix.
    private const double FullRunThresholdSeconds = 1.0;

    private static int Main(string[] args)
    {
        EnsureReleaseBuild();

        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            PrintUsage();

            return 0;
        }

        string sourceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "source");
        string snapshotsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snapshots");
        string manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gold.json");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(snapshotsDirectory);

        GoldManifest manifest = GoldManifest.Load(manifestPath);

        // A font provider is used to substitute system fonts for fonts not embedded in the PDF, and
        // PdfDocumentReader is the entry point for parsing PDF files.
        IFontProvider fontProvider = new WindowsFontProvider(NullLoggerFactory.Instance);
        PdfDocumentReader reader = new(NullLoggerFactory.Instance, fontProvider);

        if (args.Length > 0 && args[0] == "add")
        {
            return Add(reader, manifest, manifestPath, sourceDirectory, snapshotsDirectory, args.AsSpan(1).ToArray());
        }

        // A run compares unless it is asked to generate; every remaining argument that is not the
        // full-run switch selects the PDFs to process.
        bool generate = args.Length > 0 && args[0] == "generate";
        bool hasMode = generate || (args.Length > 0 && args[0] == "compare");
        string[] runArguments = hasMode ? args.AsSpan(1).ToArray() : args;

        bool fullRun = false;
        bool inspect = false;
        List<string> filters = new();

        foreach (string argument in runArguments)
        {
            if (string.Equals(argument, "--fullRun", StringComparison.OrdinalIgnoreCase))
            {
                fullRun = true;
            }
            else if (string.Equals(argument, "--inspect", StringComparison.OrdinalIgnoreCase))
            {
                inspect = true;
            }
            else
            {
                filters.Add(argument);
            }
        }

        List<GoldEntry> entries = SelectEntries(manifest, fullRun, filters);

        if (entries.Count == 0)
        {
            Write(ConsoleColor.Red, $"No PDF to process; {manifest.Files.Count} PDF(s) are registered in {manifestPath}.");

            return 1;
        }

        // Every differing page found by this run is collected flat into "Inspect", starting from a
        // clean slate, so a previous run's leftovers never get mixed in with this one's.
        string? inspectDirectory = null;

        if (inspect && !generate)
        {
            inspectDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Inspect");

            if (Directory.Exists(inspectDirectory))
            {
                Directory.Delete(inspectDirectory, recursive: true);
            }

            Directory.CreateDirectory(inspectDirectory);
        }

        Stopwatch runStopwatch = Stopwatch.StartNew();
        int differentCount = 0;
        int failedCount = 0;

        foreach (GoldEntry entry in entries)
        {
            string pdfPath = Path.Combine(sourceDirectory, entry.Name);
            string snapshotDirectory = Path.Combine(snapshotsDirectory, Path.GetFileNameWithoutExtension(entry.Name));
            Directory.CreateDirectory(snapshotDirectory);

            try
            {
                if (generate)
                {
                    Generate(reader, pdfPath, snapshotDirectory, entry.Pages, entry.Password);
                }
                else if (!Compare(reader, pdfPath, snapshotDirectory, entry.Pages, entry.Password, inspectDirectory))
                {
                    differentCount++;
                }
            }
            catch (Exception exception)
            {
                failedCount++;
                Write(ConsoleColor.Magenta, $"{entry.Name,-60} FAILED {exception.Message}");
            }
        }

        runStopwatch.Stop();

        int problemCount = differentCount + failedCount;
        string scope = filters.Count > 0 ? "selected" : fullRun ? "full" : "short";
        string summary = generate
            ? $"Generated {scope} snapshots for {entries.Count - failedCount} of {entries.Count} PDF(s) in {runStopwatch.Elapsed.TotalSeconds:F1} s; {failedCount} failed."
            : $"{entries.Count - problemCount} of {entries.Count} PDF(s) match in the {scope} run in {runStopwatch.Elapsed.TotalSeconds:F1} s; {differentCount} differ, {failedCount} failed.";

        Write(problemCount == 0 ? ConsoleColor.Green : ConsoleColor.Red, summary);

        return problemCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// Registers a single PDF. The PDF is rendered once to time it, which decides whether it is a
    /// full-run member, and golden snapshots are written for the pages that do not have one yet.
    /// </summary>
    private static int Add(
        PdfDocumentReader reader,
        GoldManifest manifest,
        string manifestPath,
        string sourceDirectory,
        string snapshotsDirectory,
        string[] arguments)
    {
        string? password = null;
        List<string> positional = new();

        for (int index = 0; index < arguments.Length; index++)
        {
            if (!string.Equals(arguments[index], "--password", StringComparison.OrdinalIgnoreCase))
            {
                positional.Add(arguments[index]);

                continue;
            }

            if (index + 1 == arguments.Length)
            {
                Write(ConsoleColor.Red, "--password needs the user password of the PDF as its value.");

                return 1;
            }

            password = arguments[index + 1];
            index++;
        }

        if (positional.Count == 0 || positional.Count > 2)
        {
            Write(ConsoleColor.Red, "Usage: add <pdf path or name> [comma separated page numbers] [--password <user password>]");

            return 1;
        }

        string fileName = Path.GetFileName(positional[0]);

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".pdf";
        }

        string pdfPath = Path.Combine(sourceDirectory, fileName);

        // A PDF given by a path outside the corpus is copied in, so that a later run can find it.
        if (File.Exists(positional[0])
            && !string.Equals(Path.GetFullPath(positional[0]), pdfPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(positional[0], pdfPath, overwrite: true);
        }

        if (!File.Exists(pdfPath))
        {
            Write(ConsoleColor.Red, $"{positional[0]} was not found, and {fileName} is not in {sourceDirectory}.");

            return 1;
        }

        List<int> pages = new();

        if (positional.Count == 2)
        {
            foreach (string part in positional[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, out int pageNumber) || pageNumber < 1)
                {
                    Write(ConsoleColor.Red, $"'{part}' is not a page number; pass 1-based page numbers as 1,2,3.");

                    return 1;
                }

                if (!pages.Contains(pageNumber))
                {
                    pages.Add(pageNumber);
                }
            }

            pages.Sort();
        }

        string snapshotDirectory = Path.Combine(snapshotsDirectory, Path.GetFileNameWithoutExtension(fileName));

        // The registration is timed to classify the PDF, so the clock covers the rendering only and
        // is held while a golden is written — a run never pays that cost.
        Stopwatch stopwatch = new();
        int pageCount = 0;
        int writtenCount = 0;

        try
        {
            stopwatch.Start();

            foreach ((int PageNumber, SKBitmap Bitmap) renderedPage in PageRenderer.RenderPages(reader, pdfPath, pages, password))
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
                    PageRenderer.SavePng(bitmap, goldenPath);
                    writtenCount++;
                }

                stopwatch.Start();
            }

            stopwatch.Stop();
        }
        catch (Exception exception)
        {
            Write(ConsoleColor.Magenta, $"{fileName,-60} FAILED {exception.Message}");

            return 1;
        }

        // A document that renders nothing would be registered with no golden at all, and every later
        // comparison of it would pass without having looked at anything.
        if (pageCount == 0)
        {
            Write(ConsoleColor.Magenta, $"{fileName,-60} FAILED the document rendered no page, so there is nothing to compare against.");

            return 1;
        }

        bool fullRunOnly = stopwatch.Elapsed.TotalSeconds > FullRunThresholdSeconds;

        GoldEntry entry = new()
        {
            Name = fileName,
            Pages = pages,
            FullRun = fullRunOnly ? true : null,
            Password = password,
        };

        manifest.Files.RemoveAll(existing => string.Equals(existing.Name, fileName, StringComparison.OrdinalIgnoreCase));
        manifest.Files.Add(entry);
        manifest.Save(manifestPath);

        string pageScope = pages.Count == 0 ? "all pages" : $"page(s) {string.Join(", ", pages)}";
        Write(
            ConsoleColor.Green,
            $"{fileName,-60} {pageScope}, {stopwatch.Elapsed.TotalSeconds:F2} s, {(fullRunOnly ? "full run only" : "short + full run")}, {writtenCount} golden(s) written");

        return 0;
    }

    private static void Generate(PdfDocumentReader reader, string pdfPath, string snapshotDirectory, List<int> pages, string? password)
    {
        int pageCount = 0;

        foreach ((int PageNumber, SKBitmap Bitmap) renderedPage in PageRenderer.RenderPages(reader, pdfPath, pages, password))
        {
            using SKBitmap bitmap = renderedPage.Bitmap;

            PageRenderer.SavePng(bitmap, Path.Combine(snapshotDirectory, $"{renderedPage.PageNumber}_source.png"));

            // The previous run's comparison output describes the snapshot that was just replaced.
            File.Delete(Path.Combine(snapshotDirectory, $"{renderedPage.PageNumber}_result.png"));
            pageCount++;
        }

        Write(ConsoleColor.Green, $"{Path.GetFileName(pdfPath),-60} {pageCount} page(s)");
    }

    private static bool Compare(PdfDocumentReader reader, string pdfPath, string snapshotDirectory, List<int> pages, string? password, string? inspectDirectory)
    {
        string pdfName = Path.GetFileName(pdfPath);
        bool matched = true;

        foreach ((int PageNumber, SKBitmap Bitmap) renderedPage in PageRenderer.RenderPages(reader, pdfPath, pages, password))
        {
            using SKBitmap page = renderedPage.Bitmap;
            int pageNumber = renderedPage.PageNumber;
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

            if (inspectDirectory != null)
            {
                CopyToInspect(inspectDirectory, pdfPath, pdfName, pageNumber, sourcePath, resultPath, golden, page);
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

        using SKBitmap diff = PageRenderer.CreateDiffImage(golden, rendered);
        PageRenderer.SavePng(diff, Path.Combine(inspectDirectory, $"{stem}_{pageNumber}_diff.png"));
    }

    // A short run leaves out the PDFs registered as full-run members; an explicit filter overrides
    // that and selects registered PDFs by file name with or without the extension, so both
    // "basicapi" and "bug1*.pdf" work.
    private static List<GoldEntry> SelectEntries(GoldManifest manifest, bool fullRun, List<string> filters)
    {
        List<GoldEntry> entries = new();

        foreach (GoldEntry entry in manifest.Files)
        {
            if (filters.Count == 0)
            {
                if (fullRun || entry.FullRun != true)
                {
                    entries.Add(entry);
                }
            }
            else if (Matches(entry.Name, filters))
            {
                entries.Add(entry);
            }
        }

        entries.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        return entries;
    }

    private static bool Matches(string fileName, List<string> filters)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);

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

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: PdfPixel.Gold [add|generate|compare] [options]");
        Console.WriteLine("  <no arguments>          compares the short set against its golden snapshots");
        Console.WriteLine("  --fullRun               compares every registered PDF, slow ones included");
        Console.WriteLine("  --inspect               also collects every differing page into the Inspect folder");
        Console.WriteLine("  a* b                    compares the selected PDFs only, short or full alike");
        Console.WriteLine("  generate                rewrites the golden snapshots of the short set");
        Console.WriteLine("  generate --fullRun      rewrites the golden snapshots of every registered PDF");
        Console.WriteLine("  generate a* b           rewrites the golden snapshots of the selected PDFs only");
        Console.WriteLine("  add <pdf> [pages] [--password <user password>]");
        Console.WriteLine("                          registers one PDF in gold.json");
        Console.WriteLine();
        Console.WriteLine("The PDF given to add is copied into the source folder when it is a path from elsewhere.");
        Console.WriteLine($"It is rendered once to time it; slower than {FullRunThresholdSeconds:F1} s registers it as a full-run member.");
        Console.WriteLine("Pages are 1-based and comma separated, as in \"1,2,5\"; leaving them out registers every page.");
        Console.WriteLine("An encrypted PDF needs --password; the password is stored in gold.json and reused by every run.");
        Console.WriteLine("A golden snapshot is written for every registered page that has none; existing goldens are kept.");
        Console.WriteLine("With --inspect, the Inspect folder is cleared and refilled with, per differing page,");
        Console.WriteLine("the source PDF and its golden/result/diff PNGs, flat and named by PDF and page.");
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

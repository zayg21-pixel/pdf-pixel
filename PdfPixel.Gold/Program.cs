using PdfPixel.Gold.Manifest;
using PdfPixel.Gold.Commands;
using PdfPixel.Gold.Utilities;

namespace PdfPixel.Gold;

/// <summary>
/// Keeps golden PNG snapshots of the PDFs registered in "gold.json" and dispatches each command to
/// its class.
/// </summary>
internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        EnsureReleaseBuild();

        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            PrintUsage();

            return args.Length == 0 ? 1 : 0;
        }

        GoldPaths paths = new(args[0]);

        if (!File.Exists(paths.ManifestPath))
        {
            ConsoleUtilities.Write(ConsoleColor.Red, $"{paths.WorkingDirectory} has no gold.json; the first argument is the working directory.");

            return 1;
        }

        GoldManifest manifest = GoldManifest.Load(paths.ManifestPath);
        string[] runArguments = args[1..];
        string command = runArguments.Length > 0 ? runArguments[0] : string.Empty;
        string[] commandArguments = runArguments.Length > 0 ? runArguments[1..] : runArguments;

        if (command == "setup")
        {
            return await SetupCommand.RunAsync(paths, manifest);
        }

        if (command == "add")
        {
            return await AddCommand.RunAsync(paths, PdfUtilities.CreateReader(), manifest, commandArguments);
        }

        if (command == "remove")
        {
            return RemoveCommand.Run(paths, manifest, commandArguments);
        }

        // A run compares unless it is asked to generate or to analyze; every remaining argument that
        // is not a switch selects the PDFs to process.
        bool hasMode = command == "generate" || command == "analyze" || command == "compare";
        RunOptions options = RunOptions.Parse(hasMode ? commandArguments : runArguments);
        List<GoldEntry> entries = manifest.SelectEntries(options.FullRun, options.Filters);

        if (entries.Count == 0)
        {
            ConsoleUtilities.Write(ConsoleColor.Red, $"No PDF to process; {manifest.Files.Count} PDF(s) are registered in {paths.ManifestPath}.");

            return 1;
        }

        PdfDocumentReader reader = PdfUtilities.CreateReader();

        if (command == "generate")
        {
            return GenerateCommand.Run(paths, reader, entries, options);
        }

        if (command == "analyze")
        {
            return AnalyzeCommand.Run(paths, reader, entries, options);
        }

        return CompareCommand.Run(paths, reader, entries, options);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: PdfPixel.Gold <working directory> [setup|add|remove|generate|analyze|compare] [options]");
        Console.WriteLine("  <working directory>     folder holding gold.json, with Source, Snapshots and Inspect below it");
        Console.WriteLine("  setup                   downloads every PDF in gold.json that is not in the Source folder yet");
        Console.WriteLine("  <no command>            compares the short set against its golden snapshots");
        Console.WriteLine("  --full                  compares every registered PDF, slow ones included");
        Console.WriteLine("  --inspect               also collects every differing page into the Inspect folder");
        Console.WriteLine("  a* b                    compares the selected PDFs only, short or full alike");
        Console.WriteLine("  generate                rewrites the golden snapshots of the short set");
        Console.WriteLine("  generate --full         rewrites the golden snapshots of every registered PDF");
        Console.WriteLine("  generate a* b           rewrites the golden snapshots of the selected PDFs only");
        Console.WriteLine("  analyze                 renders the short set and prints how long every page took");
        Console.WriteLine("  analyze --full          renders every registered PDF and prints its statistics");
        Console.WriteLine("  analyze a* b            renders the selected PDFs only and prints their statistics");
        Console.WriteLine("  analyze --memory        also measures the peak and the retained memory of every page");
        Console.WriteLine("  add <pdf name> <url> [pages] [--password <user password>] [--origin <url>]");
        Console.WriteLine("                          downloads one PDF and registers it in gold.json");
        Console.WriteLine("  remove <pdf name>       unregisters one PDF and deletes its source and snapshots");
        Console.WriteLine();
        Console.WriteLine("Setup keeps a download only when its SHA-256 matches gold.json; a file already present is skipped.");
        Console.WriteLine($"Add renders the PDF once to time it; slower than {AddCommand.FullRunThresholdSeconds:F1} s registers it as a full-run member.");
        Console.WriteLine("Pages are 1-based and comma separated, as in \"1,2,5\"; leaving them out registers every page.");
        Console.WriteLine("An encrypted PDF needs --password; the password is stored in gold.json and reused by every run.");
        Console.WriteLine("A golden snapshot is written for every registered page that has none; existing goldens are kept.");
        Console.WriteLine("With --inspect, the Inspect folder is cleared and refilled with, per differing page,");
        Console.WriteLine("the source PDF and its golden/result/diff PNGs, flat and named by PDF and page.");
        Console.WriteLine("Analyze renders to measure only: it compares no page and writes no file, and it closes");
        Console.WriteLine("with the slow pages, followed by the memory hungry ones when --memory is given.");
    }

    // Snapshots taken from a debug build are not comparable with the ones taken from a release build.
    private static void EnsureReleaseBuild()
    {
#if DEBUG
        throw new InvalidOperationException("PdfPixel.Gold runs in Release only. Build the release configuration and run it from bin\\Release.");
#endif
    }
}

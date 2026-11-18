using Microsoft.Extensions.Logging;
using PdfPixel.Diagnostics;
using PdfPixel.PanelDiagnostics.Utilities;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Fonts.Substitution;
using System.Diagnostics;

namespace PdfPixel.PanelDiagnostics.Commands;

/// <summary>
/// Replays a script of user interactions against a <see cref="PdfPanelContext"/>, logging script commands,
/// tile changes and the time the panel thread is blocked.
/// </summary>
internal static class ReplayCommand
{
    private const int DefaultPanelWidth = 1280;
    private const int DefaultPanelHeight = 960;

    /// <summary>
    /// Opens <paramref name="pdfFile"/> in a panel and replays <paramref name="scriptFile"/> against it, profiled as
    /// <paramref name="profile"/> asks, the trace written next to the script.
    /// </summary>
    public static void Run(FileInfo pdfFile, FileInfo scriptFile, ProfileMode profile)
    {
        List<PanelScriptLine> script = PanelScriptLine.ParseFile(scriptFile.FullName);

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        PdfDocumentReader reader = new(loggerFactory, PdfFontSubstitutorFactory.Create(loggerFactory));
        using IPdfDocument document = reader.Read(new MemoryStream(File.ReadAllBytes(pdfFile.FullName)));

        Stopwatch clock = Stopwatch.StartNew();
        using PanelSynchronizationContext synchronizationContext = new(clock);
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);

        using PdfPanelDocument panelDocument = PdfPanelDocument.FromDocument(document, loggerFactory, synchronizationContext);
        PanelTileLog tileLog = new(clock);
        using PdfPanelContext context = new(panelDocument, tileLog, new PanelThumbnailResourceFactory());
        context.Tiles.TilesChanged += tileLog.OnTilesChanged;

        // TODO: temporary tile size override for the retained-memory investigation, remove when done.
        string? tileSize = Environment.GetEnvironmentVariable("PDFPIXEL_TILE_SIZE");

        if (tileSize != null)
        {
            context.Renderer.TileSize = int.Parse(tileSize, System.Globalization.CultureInfo.InvariantCulture);
        }

        context.PanelWidth = DefaultPanelWidth;
        context.PanelHeight = DefaultPanelHeight;

        void Replay()
        {
            Execute(context, clock, "open", null);

            foreach (PanelScriptLine line in script)
            {
                if (line.Name == "sleep")
                {
                    Console.WriteLine($"{clock.Elapsed.TotalMilliseconds,10:F1} ms  > {line.Text}");
                    synchronizationContext.Run(TimeSpan.FromSeconds(line.Arguments[0]));
                    continue;
                }

                Execute(context, clock, line.Text, line);
            }
        }

        string outputDirectory = scriptFile.DirectoryName ?? Environment.CurrentDirectory;

        if (profile == ProfileMode.None || profile == ProfileMode.Heap)
        {
            Replay();
        }
        else
        {
            Profiler.Collect(profile, outputDirectory, Replay);
        }

        if (profile == ProfileMode.Heap)
        {
            Profiler.CollectHeapDump(outputDirectory);
        }
    }

    /// <summary>
    /// Applies <paramref name="line"/> to <paramref name="context"/>, when one is given, then synchronizes and renders the panel,
    /// logging the time it took.
    /// </summary>
    private static void Execute(PdfPanelContext context, Stopwatch clock, string text, PanelScriptLine? line)
    {
        TimeSpan start = clock.Elapsed;
        Console.WriteLine($"{start.TotalMilliseconds,10:F1} ms  > {text}");

        if (line != null)
        {
            Apply(context, line);
        }

        context.Synchronize();
        context.Render();

        TimeSpan end = clock.Elapsed;
        Console.WriteLine($"{end.TotalMilliseconds,10:F1} ms  < {text} took {(end - start).TotalMilliseconds:F1} ms");
    }

    private static void Apply(PdfPanelContext context, PanelScriptLine line)
    {
        float[] arguments = line.Arguments;

        switch (line.Name)
        {
            case "size":
                context.PanelWidth = arguments[0];
                context.PanelHeight = arguments[1];
                break;
            case "zoom":
                context.Zoom(arguments[0], context.PanelWidth / 2, context.PanelHeight / 2);
                break;
            case "scroll":
                context.HorizontalOffset = arguments[0];
                context.VerticalOffset = arguments[1];
                break;
            case "scroll_relative":
                context.HorizontalOffset += arguments[0];
                context.VerticalOffset += arguments[1];
                break;
            case "page":
                context.ScrollToPage((int)arguments[0]);
                break;
        }
    }
}

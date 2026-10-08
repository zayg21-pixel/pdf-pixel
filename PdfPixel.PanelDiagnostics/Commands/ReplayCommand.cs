using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.PanelDiagnostics.Utilities;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Skia.Fonts;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.PanelDiagnostics.Commands;

/// <summary>
/// Replays a script of user interactions against a <see cref="PdfPanelContext"/>, logging script commands,
/// presented frames and the time the panel thread is blocked.
/// </summary>
internal static class ReplayCommand
{
    private const int DefaultPanelWidth = 1280;
    private const int DefaultPanelHeight = 960;

    private static readonly TimeSpan YieldInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>
    /// Opens <paramref name="pdfFile"/> in a panel decoding with <paramref name="queueMode"/> and replays <paramref name="scriptFile"/> against it.
    /// </summary>
    public static void Run(FileInfo pdfFile, FileInfo scriptFile, WorkQueueMode queueMode)
    {
        List<PanelScriptLine> script = PanelScriptLine.ParseFile(scriptFile.FullName);

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        PdfDocumentReader reader = new(loggerFactory, new SkiaFontSubstitutor(loggerFactory));
        using IPdfDocument document = reader.Read(new MemoryStream(File.ReadAllBytes(pdfFile.FullName)));

        Stopwatch clock = Stopwatch.StartNew();
        using PanelSynchronizationContext synchronizationContext = new(clock);
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);

        using PdfPanelPageCollection pages = CreatePages(document, queueMode, loggerFactory);
        using CpuSkSurfaceFactory surfaceFactory = new(SKColorType.Rgba8888, SKAlphaType.Premul);
        PanelRenderTarget renderTarget = new(clock);
        using PdfPanelContext context = new(pages, surfaceFactory, renderTarget, synchronizationContext);

        context.PanelWidth = DefaultPanelWidth;
        context.PanelHeight = DefaultPanelHeight;
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

    private static PdfPanelPageCollection CreatePages(IPdfDocument document, WorkQueueMode queueMode, ILoggerFactory loggerFactory)
    {
        if (queueMode == WorkQueueMode.Immediate)
        {
            return PdfPanelPageCollection.FromDocument(
                document,
                new ImmidiateWorkQueue(loggerFactory.CreateLogger<ImmidiateWorkQueue>()),
                new PdfYieldingObserverFactory(YieldInterval));
        }

        return PdfPanelPageCollection.FromDocument(document, loggerFactory);
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

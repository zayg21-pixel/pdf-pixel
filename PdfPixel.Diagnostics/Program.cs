using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Skia;
using PdfPixel.Fonts.Management;
using PdfPixel.Skia.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;
using System.CommandLine;
using System.Diagnostics;
using System.Linq;
using PdfPixel.Commands.Model;
using PdfPixel.Commands.Context;

namespace PdfPixel.Diagnostics;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        Argument<FileInfo> pdfArgument = new("pdf")
        {
            Description = "Path to the PDF to process."
        };

        Option<float> scaleOption = new("--scale", "-s")
        {
            Description = "Multiplies the page size before rendering; above 1 gives a sharper image.",
            DefaultValueFactory = _ => 4f
        };

        Option<int> iterationsOption = new("--iterations", "-n")
        {
            Description = "Times each page is processed, to get a stable average.",
            DefaultValueFactory = _ => 10
        };

        Option<int> minPageOption = new("--min-page", "-f")
        {
            Description = "First page to process, 1-based.",
            DefaultValueFactory = _ => 1
        };

        Option<int?> maxPageOption = new("--max-page", "-l")
        {
            Description = "Last page to process, 1-based. Defaults to the final page."
        };

        Option<bool> rasterizeOption = new("--rasterize")
        {
            Description = "Rasterize the recorded page to a surface. Use --rasterize:false to measure decoding alone.",
            DefaultValueFactory = _ => true
        };

        Option<bool> savePngOption = new("--save-png")
        {
            Description = "Write a PNG snapshot of each page. Requires rasterization.",
            DefaultValueFactory = _ => true
        };

        Option<bool> dumpCommandsOption = new("--dump-commands")
        {
            Description = "Print every recorded drawing command."
        };

        Option<string?> passwordOption = new("--password")
        {
            Description = "Password used to decrypt the PDF, when it is encrypted."
        };

        Option<ProfileMode> profileOption = new("--profile")
        {
            Description = "Profiles the run: 'cpu' reports the methods the time went to, 'memory' the types allocated.",
            DefaultValueFactory = _ => ProfileMode.None
        };

        RootCommand rootCommand = new("Records and replays a PDF page, reporting decode and rasterization timings.")
        {
            pdfArgument,
            scaleOption,
            iterationsOption,
            minPageOption,
            maxPageOption,
            rasterizeOption,
            savePngOption,
            dumpCommandsOption,
            passwordOption,
            profileOption
        };

        rootCommand.SetAction(parseResult =>
        {
            Run(
                parseResult.GetValue(pdfArgument),
                parseResult.GetValue(scaleOption),
                parseResult.GetValue(iterationsOption),
                parseResult.GetValue(minPageOption),
                parseResult.GetValue(maxPageOption),
                parseResult.GetValue(rasterizeOption),
                parseResult.GetValue(savePngOption),
                parseResult.GetValue(dumpCommandsOption),
                parseResult.GetValue(passwordOption),
                parseResult.GetValue(profileOption));

            return 0;
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static void Run(
        FileInfo? pdfFile,
        float scale,
        int iterationCount,
        int minPage,
        int? maxPage,
        bool rasterize,
        bool savePng,
        bool dumpCommands,
        string? password,
        ProfileMode profile)
    {
        if (pdfFile == null)
        {
            return;
        }

        string pdfPath = pdfFile.FullName;

        // PdfDocumentReader needs a logger factory for diagnostics during parsing and rendering.
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = loggerFactory.CreateLogger<Program>();

        // ...and a font provider, used to substitute system fonts for fonts not embedded in the PDF.
        SkiaFontSubstitutor fontSubstitutor = new(loggerFactory);
        FontProvider fontProvider = new(fontSubstitutor, loggerFactory);

        // PdfDocumentReader is the entry point for parsing PDF files.
        PdfDocumentReader reader = new(loggerFactory, fontProvider);

        // Each PDF gets its own output subfolder, named after the source file, under pdfs/.
        string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfs", Path.GetFileNameWithoutExtension(pdfPath));
        Directory.CreateDirectory(outputDirectory);

        int pageCount = GetPageCount(reader, pdfPath, password);
        int firstPage = Math.Max(1, minPage);
        int lastPage = Math.Min(maxPage ?? pageCount, pageCount);

        if (firstPage > lastPage)
        {
            logger.LogError("Page range {FirstPage}-{LastPage} is empty; the document has {PageCount} page(s).", firstPage, lastPage, pageCount);
            return;
        }

        void ProcessPages()
        {
            for (int pageNumber = firstPage; pageNumber <= lastPage; pageNumber++)
            {
                MeasurePage(
                    reader,
                    loggerFactory,
                    logger,
                    pdfPath,
                    outputDirectory,
                    pageNumber,
                    scale,
                    iterationCount,
                    rasterize,
                    savePng,
                    dumpCommands,
                    password,
                    profile);
            }
        }

        if (profile == ProfileMode.None || profile == ProfileMode.Heap)
        {
            ProcessPages();

            return;
        }

        Profiler.Collect(profile, outputDirectory, ProcessPages);
    }

    private static int GetPageCount(PdfDocumentReader reader, string pdfPath, string? password)
    {
        using FileStream fileStream = File.OpenRead(pdfPath);
        using IPdfDocument document = reader.Read(fileStream, password);
        return document.Pages.Count;
    }

    private static void MeasurePage(
        PdfDocumentReader reader,
        ILoggerFactory loggerFactory,
        ILogger logger,
        string pdfPath,
        string outputDirectory,
        int pageNumber,
        float scale,
        int iterationCount,
        bool rasterize,
        bool savePng,
        bool dumpCommands,
        string? password,
        ProfileMode profile)
    {
        double[] totalMilliseconds = new double[iterationCount];
        double[] decodeMilliseconds = new double[iterationCount];
        double[] rasterMilliseconds = new double[iterationCount];

        for (int iteration = 0; iteration < iterationCount; iteration++)
        {
            // Re-open and re-parse the document every iteration so no per-document decode cache
            // (e.g. PdfDocumentObjectCache.Images) can mask real decode cost on later iterations.
            using FileStream fileStream = File.OpenRead(pdfPath);
            using IPdfDocument document = reader.Read(fileStream, password);

            IPdfPage page = document.Pages[pageNumber - 1];

            Stopwatch decodeStopwatch = Stopwatch.StartNew();
            Stopwatch rasterStopwatch = new();

            // Guards the page's lazily-parsed content stream against concurrent access; use a private
            // object per concurrent render of the same page.
            object contentLocker = new();

            // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
            IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

            // Pass 1: record the page's drawing commands without executing them. No canvas exists
            // yet at this point; PdfCommandRecorder just collects the commands for later replay.
            // Content and annotations are recorded into separate recorders so they can be dumped
            // and replayed independently.
            PdfCommandRecorder contentRecorder = new();
            RecordPageTransform(contentRecorder, page, scale);
            PdfRenderingParameters renderingParameters = new()
            {
                RenderImages = Environment.GetEnvironmentVariable("MEMPROBE_NO_IMAGES") == null,
                RenderText = Environment.GetEnvironmentVariable("MEMPROBE_NO_TEXT") == null,
            };
            page.Render(contentRecorder, renderingParameters, executionObserver);
            contentRecorder.Process(RestoreStateCommand.Instance);

            PdfCommandRecorder annotationRecorder = new();
            RecordPageTransform(annotationRecorder, page, scale);

            // Annotations (comments, stamps, links, etc.) are recorded separately from page content.
            foreach (PdfPageAnnotation annotation in page.Annotations)
            {
                // Skip annotations excluded from on-screen and print rendering.
                if ((annotation.Content.Flags & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0)
                {
                    continue;
                }

                annotation.Render(annotationRecorder, PdfAnnotationVisualStateKind.Normal, new PdfRenderingParameters(), executionObserver);
            }

            annotationRecorder.Process(RestoreStateCommand.Instance);

            if (dumpCommands && iteration == 0)
            {
                Console.WriteLine($"Page {page.PageNumber} content commands:");
                DumpCommands(contentRecorder.Commands);

                Console.WriteLine($"Page {page.PageNumber} annotation commands:");
                DumpCommands(annotationRecorder.Commands);
            }

            // Pass 2: replay the recorded commands against a picture-recording canvas. Decoding
            // happens here; recording defers the drawing itself to the rasterization pass below,
            // which keeps the two costs separately measurable.
            // CropBox is the visible page area in PDF units; scale it to get the output image size.
            SKImageInfo imageInfo = new((int)(page.CropBox.Width * scale), (int)(page.CropBox.Height * scale));
            using SKPictureRecorder pictureRecorder = new();
            SKCanvas canvas = pictureRecorder.BeginRecording(new SKRect(0, 0, imageInfo.Width, imageInfo.Height));

            // Bundles the canvas, rendering options, and the objects above into the state every
            // drawing command reads from while the recordings are replayed.
            using PdfCommandExecutionContext executionContext = new(
                document,
                new PdfCommandExecutionParameters(),
                contentLocker,
                document.OptionalContentGroups,
                executionObserver);

            SkCanvasCommandProcessor processor = new(canvas, executionContext, loggerFactory.CreateLogger<SkCanvasCommandProcessor>());
            contentRecorder.Replay(processor);
            annotationRecorder.Replay(processor);

            using SKPicture picture = pictureRecorder.EndRecording();

            decodeStopwatch.Stop();

            // Pass 3: rasterize the recorded picture. Everything the page needs has already been
            // decoded, so this measures Skia's CPU rasterization on its own.
            if (rasterize)
            {
                rasterStopwatch.Start();

                using SKSurface surface = SKSurface.Create(imageInfo);
                SKCanvas surfaceCanvas = surface.Canvas;
                surfaceCanvas.Clear(SKColors.White);
                surfaceCanvas.DrawPicture(picture);
                surfaceCanvas.Flush();

                rasterStopwatch.Stop();

                if (savePng && iteration == 0)
                {
                    SavePageSnapshot(logger, surface, outputDirectory, page.PageNumber);
                }
            }

            if (profile == ProfileMode.Heap && iteration == iterationCount - 1)
            {
                Profiler.CollectHeapDump(outputDirectory);
            }

            decodeMilliseconds[iteration] = decodeStopwatch.Elapsed.TotalMilliseconds;
            rasterMilliseconds[iteration] = rasterStopwatch.Elapsed.TotalMilliseconds;
            totalMilliseconds[iteration] = decodeMilliseconds[iteration] + rasterMilliseconds[iteration];

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (Environment.GetEnvironmentVariable("MEMPROBE_PURGE") != null)
            {
                SKGraphics.PurgeAllCaches();
            }

            using Process currentProcess = Process.GetCurrentProcess();
            Console.WriteLine(
                $"MEMPROBE iteration {iteration,-3}"
                    + $" private {currentProcess.PrivateMemorySize64 / 1048576.0,8:F1} MB"
                    + $" gcCommitted {GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0,8:F1} MB"
                    + $" live {GC.GetTotalMemory(forceFullCollection: false) / 1048576.0,8:F1} MB"
                    + $" font {SKGraphics.GetFontCacheUsed() / 1048576.0,6:F1} MB"
                    + $" resource {SKGraphics.GetResourceCacheTotalBytesUsed() / 1048576.0,6:F1} MB");
        }

        logger.LogInformation(
            "Page {PageNumber} x{IterationCount} at scale {Scale}: total avg {TotalAverage:F1} ms (min {TotalMin:F1} ms) = decode avg {DecodeAverage:F1} ms (min {DecodeMin:F1} ms) + raster avg {RasterAverage:F1} ms (min {RasterMin:F1} ms)",
            pageNumber,
            iterationCount,
            scale,
            totalMilliseconds.Average(),
            totalMilliseconds.Min(),
            decodeMilliseconds.Average(),
            decodeMilliseconds.Min(),
            rasterMilliseconds.Average(),
            rasterMilliseconds.Min());
    }

    private static void SavePageSnapshot(ILogger logger, SKSurface surface, string outputDirectory, int pageNumber)
    {
        string outputPath = Path.Combine(outputDirectory, $"{pageNumber}.png");
        using SKImage image = surface.Snapshot();
        using SKData pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(outputPath);
        pngData.SaveTo(output);

        logger.LogInformation("Exported page {PageNumber} to {OutputPath}", pageNumber, outputPath);
    }

    // Save the execution context's state before applying the page transform, so it can be restored afterwards.
    // Scales the whole page up or down to the requested output resolution, then translates and flips it:
    // PDF content is authored with the origin at the bottom-left and Y increasing upward, while the canvas
    // has the origin at the top-left and Y increasing downward.
    private static void RecordPageTransform(PdfCommandRecorder recorder, IPdfPage page, float scale)
    {
        recorder.Process(SaveStateCommand.Instance);
        recorder.ApplyPageTransformations(page.CropBox, scale: scale, clipToBounds: false);
    }

    // Prints every command in order; when a command is a DrawRecordingCommand, its nested
    // recording is dumped recursively, indented one tab further per recursion level.
    private static void DumpCommands(IReadOnlyList<IPdfCommand> commands, int depth = 0)
    {
        string indent = new('\t', depth);

        foreach (IPdfCommand command in commands)
        {
            Console.WriteLine($"{indent}{command}");

            if (command is DrawRecordingCommand recordingCommand)
            {
                DumpCommands(recordingCommand.Recorder.Commands, depth + 1);
            }
        }
    }
}

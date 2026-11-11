using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Geometry;
using System.CommandLine;
using System.Diagnostics;
using System.Linq;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Execution;
using PdfPixel.OptionalContent;
using PdfPixel.Text;
using PdfPixel.Encryption;
using PdfPixel.Imaging.Encoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap;

namespace PdfPixel.Diagnostics;

internal sealed class Program
{
    // Each output sample is one byte, as the PNG snapshot and the over-white step read them.
    private const int OutputBitsPerComponent = 8;

    private static int Main(string[] args)
    {
        // TODO: Temporary hook for FontResolutionCheck; remove with it.
        if (args.Length > 0 && args[0] == "--font-resolution")
        {
            return FontResolutionCheck.Run();
        }

        Argument<FileInfo> pdfArgument = new("pdf")
        {
            Description = "Path to the PDF to process."
        };

        Option<float> scaleOption = new("--scale", "-s")
        {
            Description = "Multiplies the page size before rendering; above 1 gives a sharper image.",
            DefaultValueFactory = _ => 1f
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
            Description = "Replay the recorded page with Snap, which decodes and rasterizes it. Use --rasterize:false to measure recording alone.",
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

        Option<bool> textOnlyOption = new("--text-only")
        {
            Description = "Extracts text from every page in range without rendering, timing the whole range as one run."
        };

        Option<int?> viewportOption = new("--viewport")
        {
            Description = "Renders only a centered square of this many pixels of the scaled page, the way a viewer draws its visible area."
        };

        Option<bool> cacheTilesOption = new("--cache-tiles")
        {
            Description = "Keeps decoded image tiles across draws, the way a viewer records pages, instead of dropping each tile once drawn."
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
            profileOption,
            textOnlyOption,
            viewportOption,
            cacheTilesOption
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
                parseResult.GetValue(profileOption),
                parseResult.GetValue(textOnlyOption),
                parseResult.GetValue(viewportOption),
                parseResult.GetValue(cacheTilesOption));

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
        ProfileMode profile,
        bool textOnly,
        int? viewport,
        bool cacheTiles)
    {
        if (pdfFile == null)
        {
            return;
        }

        string pdfPath = pdfFile.FullName;

        // PdfDocumentReader needs a logger factory for diagnostics during parsing and rendering.
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = loggerFactory.CreateLogger<Program>();

        // ...and a font substitutor, used to substitute system fonts for fonts not embedded in the PDF.
        PdfFontSubstitutor fontSubstitutor = PdfFontSubstitutorFactory.Create(loggerFactory);

        // PdfDocumentReader is the entry point for parsing PDF files.
        PdfDocumentReader reader = new(loggerFactory, fontSubstitutor);

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

        // Per-page timings of every iteration, indexed [page - firstPage][iteration], for the per-page summary.
        double[][] recordMilliseconds = new double[lastPage - firstPage + 1][];
        double[][] renderMilliseconds = new double[lastPage - firstPage + 1][];

        for (int pageIndex = 0; pageIndex < recordMilliseconds.Length; pageIndex++)
        {
            recordMilliseconds[pageIndex] = new double[iterationCount];
            renderMilliseconds[pageIndex] = new double[iterationCount];
        }

        void ProcessPages()
        {
            for (int iteration = 0; iteration < iterationCount; iteration++)
            {
                MeasureIteration(
                    reader,
                    loggerFactory,
                    logger,
                    pdfPath,
                    outputDirectory,
                    firstPage,
                    lastPage,
                    iteration,
                    iterationCount,
                    scale,
                    rasterize,
                    savePng,
                    dumpCommands,
                    password,
                    profile,
                    textOnly,
                    viewport,
                    cacheTiles,
                    recordMilliseconds,
                    renderMilliseconds);
            }

            for (int pageNumber = firstPage; pageNumber <= lastPage; pageNumber++)
            {
                double[] pageRecordMilliseconds = recordMilliseconds[pageNumber - firstPage];
                double[] pageRenderMilliseconds = renderMilliseconds[pageNumber - firstPage];
                double[] pageTotalMilliseconds = pageRecordMilliseconds.Zip(pageRenderMilliseconds, (record, render) => record + render).ToArray();

                logger.LogInformation(
                    "Page {PageNumber} x{IterationCount} at scale {Scale}: total avg {TotalAverage:F1} ms (min {TotalMin:F1} ms) = record avg {RecordAverage:F1} ms (min {RecordMin:F1} ms) + render avg {RenderAverage:F1} ms (min {RenderMin:F1} ms)",
                    pageNumber,
                    iterationCount,
                    scale,
                    pageTotalMilliseconds.Average(),
                    pageTotalMilliseconds.Min(),
                    pageRecordMilliseconds.Average(),
                    pageRecordMilliseconds.Min(),
                    pageRenderMilliseconds.Average(),
                    pageRenderMilliseconds.Min());
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
        using MemoryStream documentStream = new(File.ReadAllBytes(pdfPath));
        using IPdfDocument document = reader.Read(documentStream, request => (request.Reason == PdfCredentialRequestReason.CredentialRequired && password != null) ? new PdfPasswordCredential(password) : null);
        return document.Pages.Count;
    }

    private static void MeasureIteration(
        PdfDocumentReader reader,
        ILoggerFactory loggerFactory,
        ILogger logger,
        string pdfPath,
        string outputDirectory,
        int firstPage,
        int lastPage,
        int iteration,
        int iterationCount,
        float scale,
        bool rasterize,
        bool savePng,
        bool dumpCommands,
        string? password,
        ProfileMode profile,
        bool textOnly,
        int? viewport,
        bool cacheTiles,
        double[][] recordMilliseconds,
        double[][] renderMilliseconds)
    {
        // Re-open and re-parse the document every iteration so no per-document decode cache
        // (e.g. PdfDocumentObjectCache.Images) can mask real decode cost on later iterations.
        // Pages within one iteration share the document, the same way a viewer reads it.
        // The file is read into memory before the clock starts, so disk access stays out of the timings.
        using MemoryStream documentStream = new(File.ReadAllBytes(pdfPath));
        Stopwatch iterationStopwatch = Stopwatch.StartNew();
        using IPdfDocument document = reader.Read(documentStream,request => (request.Reason == PdfCredentialRequestReason.CredentialRequired && password != null) ? new PdfPasswordCredential(password) : null);

        // Guards the page's lazily-parsed content stream against concurrent access; use a private
        // object per concurrent render of the same page.
        object contentLocker = new();

        // Holds the values command execution reuses across every replay of the document.
        using CommandCache commandCache = new();

        // With --cache-tiles, decoded images stay for the next draws of the document; without it nothing stays after a draw.
        int imageCacheMegabytes = cacheTiles ? 32 : 0;
        PdfSnapImageCache imageCache = new(tileSize: 256, putCapacityMegabytes: imageCacheMegabytes, rentCapacityMegabytes: imageCacheMegabytes);

        // Optional content groups are the PDF's layers; the default states render every layer in its default visibility state.
        PdfOptionalContentStates optionalContentStates = document.OptionalContentProperties.GetDefaultStates();

        // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        // Text-only suppresses everything that draws and keeps text extraction alone.
        PdfRenderingParameters renderingParameters = new();

        if (textOnly)
        {
            renderingParameters.ProcessPaths = false;
            renderingParameters.ProcessImages = false;
            renderingParameters.ProcessShadings = false;
            renderingParameters.RenderContent = false;
        }

        double iterationRecordMilliseconds = 0;
        double iterationRenderMilliseconds = 0;
        long iterationCharacterCount = 0;

        // Every page's extracted words stay alive until the whole range is read, the way a search keeps them.
        List<PdfWord[]> pageWords = [];
        PdfTextExtractor textExtractor = new();
        PdfTextExtractionProperties textExtractionProperties = new();

        for (int pageNumber = firstPage; pageNumber <= lastPage; pageNumber++)
        {
            IPdfPage page = document.Pages[pageNumber - 1];

            Stopwatch recordStopwatch = Stopwatch.StartNew();
            Stopwatch replayStopwatch = new();
            Stopwatch resolveStopwatch = new();

            // Pass 1: record the page's drawing commands without executing them. No canvas exists
            // yet at this point; PdfCommandRecorder just collects the commands for later replay.
            // Content and annotations are recorded into separate recorders so they can be dumped
            // and replayed independently.
            PdfCommandRecorder contentRecorder = new();
            RecordPageTransform(contentRecorder, page, scale);
            page.Render(contentRecorder, renderingParameters, executionObserver);
            contentRecorder.Process(RestoreStateCommand.Instance);

            PdfCommandRecorder annotationRecorder = new();
            RecordPageTransform(annotationRecorder, page, scale);

            // Annotations (comments, stamps, links, etc.) are recorded separately from page content.
            // Text-only reads page content alone, so annotation text never mixes into the page's characters.
            if (!textOnly)
            {
                foreach (PdfPageAnnotation annotation in page.GetAnnotations())
                {
                    // Skip annotations excluded from on-screen and print rendering.
                    if ((annotation.Content.Flags & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0)
                    {
                        continue;
                    }

                    annotation.Render(annotationRecorder, PdfAnnotationVisualStateKind.Normal, renderingParameters, executionObserver);
                }
            }

            annotationRecorder.Process(RestoreStateCommand.Instance);

            if (dumpCommands && iteration == 0)
            {
                Console.WriteLine($"Page {page.PageNumber} content commands:");
                DumpCommands(contentRecorder.Commands);

                Console.WriteLine($"Page {page.PageNumber} annotation commands:");
                DumpCommands(annotationRecorder.Commands);
            }

            recordStopwatch.Stop();

            PdfWord[] words = [];

            // Pass 2: replay the recorded commands with Snap. Images are decoded and the page is
            // rasterized during this replay; resolving the surface into RGBA is timed apart from it.
            if (rasterize || textOnly)
            {
                // CropBox is the visible page area in PDF units; scale it to get the output image size.
                int pageWidth = (int)(page.CropBox.Width * scale);
                int pageHeight = (int)(page.CropBox.Height * scale);

                // The visible area: the whole page, or a centered viewport square of it.
                PdfIntegerRectangle bounds = new(0, 0, pageWidth, pageHeight);
                PdfRectangle? visibleRegion = null;

                if (viewport != null)
                {
                    int viewportWidth = Math.Min(viewport.Value, pageWidth);
                    int viewportHeight = Math.Min(viewport.Value, pageHeight);
                    int viewportLeft = (pageWidth - viewportWidth) / 2;
                    int viewportTop = (pageHeight - viewportHeight) / 2;
                    bounds = new PdfIntegerRectangle(viewportLeft, viewportTop, viewportLeft + viewportWidth, viewportTop + viewportHeight);
                    // The surface is translated by the viewport's top-left corner, so in its device space the viewport starts at the origin.
                    visibleRegion = new PdfRectangle(0, 0, viewportWidth, viewportHeight);
                }

                // Text-only replays onto a single pixel, so nothing is drawn while the content tree is built.
                if (textOnly)
                {
                    bounds = new PdfIntegerRectangle(0, 0, 1, 1);
                }

                // Bundles the rendering options and the objects above into the state every drawing
                // command reads from while the recordings are replayed.
                PdfCommandExecutionContext executionContext = new(
                    commandCache,
                    new PdfCommandExecutionParameters { DeviceProfiles = PdfDeviceProfiles.FromPage(page) },
                    contentLocker,
                    optionalContentStates,
                    PdfAnnotationStates.Empty,
                    executionObserver,
                    visibleRegion);

                replayStopwatch.Start();

                PdfSnapSurfaceParameters surfaceParameters = new(PdfDeviceRgbColorSpaceConverter.Instance, new PdfSnapMemoryPool(), imageCache);
                PdfSnapSurface surface = new(bounds.Width, bounds.Height, surfaceParameters, executionContext, loggerFactory.CreateLogger<PdfSnapSurface>());
                surface.Process(new ConcatMatrixCommand(PdfMatrix.CreateTranslation(-bounds.Left, -bounds.Top)));

                // The page content and the annotations over it are composited together as the page group.
                PdfCommandRecorder pageGroupContent = new();
                pageGroupContent.Process(new DrawRecordingCommand(contentRecorder));
                pageGroupContent.Process(new DrawRecordingCommand(annotationRecorder));
                surface.Process(page.CreatePageGroup(new DrawRecordingCommand(pageGroupContent)));

                replayStopwatch.Stop();

                // Replay built the page's content tree; extract its words in reading order.
                words = textExtractor.ExtractWords(executionContext.ContentTree.Root, PdfMatrix.Identity, textExtractionProperties);

                if (!textOnly)
                {
                    PdfImageChannel[] channels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];
                    PdfImageSamples samples = new(bounds.Width, bounds.Height, PdfImageSampleFormat.Integer, OutputBitsPerComponent, channels, isPremultiplied: true);

                    resolveStopwatch.Start();
                    surface.ToImageSamples(samples);
                    resolveStopwatch.Stop();

                    if (savePng && iteration == 0)
                    {
                        SavePageSnapshot(logger, samples, outputDirectory, page.PageNumber);
                    }
                }
            }

            int characterCount = CountCharacters(words);
            double renderElapsedMilliseconds = replayStopwatch.Elapsed.TotalMilliseconds + resolveStopwatch.Elapsed.TotalMilliseconds;

            recordMilliseconds[pageNumber - firstPage][iteration] = recordStopwatch.Elapsed.TotalMilliseconds;
            renderMilliseconds[pageNumber - firstPage][iteration] = renderElapsedMilliseconds;
            iterationRecordMilliseconds += recordStopwatch.Elapsed.TotalMilliseconds;
            iterationRenderMilliseconds += renderElapsedMilliseconds;
            iterationCharacterCount += characterCount;
            pageWords.Add(words);

            Console.WriteLine(
                $"Iteration {iteration,-3} page {pageNumber,-5}"
                    + $" total {recordStopwatch.Elapsed.TotalMilliseconds + renderElapsedMilliseconds,9:F1} ms"
                    + $" record {recordStopwatch.Elapsed.TotalMilliseconds,9:F1} ms"
                    + $" replay {replayStopwatch.Elapsed.TotalMilliseconds,9:F1} ms"
                    + $" resolve {resolveStopwatch.Elapsed.TotalMilliseconds,9:F1} ms"
                    + $" characters {characterCount,7}");
        }

        // Wall time from opening the document to the last page's characters being stored.
        iterationStopwatch.Stop();

        if (profile == ProfileMode.Heap && iteration == iterationCount - 1)
        {
            Profiler.CollectHeapDump(outputDirectory);
        }

        // Measured with the document still open and every page's characters still held.
        long liveBytes = GC.GetTotalMemory(forceFullCollection: true);
        using Process currentProcess = Process.GetCurrentProcess();

        Console.WriteLine(
            $"Iteration {iteration,-3} pages {lastPage - firstPage + 1,-5}"
                + $" wall {iterationStopwatch.Elapsed.TotalMilliseconds,9:F1} ms"
                + $" record {iterationRecordMilliseconds,9:F1} ms"
                + $" render {iterationRenderMilliseconds,9:F1} ms"
                + $" characters {iterationCharacterCount,7}"
                + $" live {liveBytes / 1048576.0,8:F1} MB"
                + $" private {currentProcess.PrivateMemorySize64 / 1048576.0,8:F1} MB"
                + $" peak private {currentProcess.PeakPagedMemorySize64 / 1048576.0,8:F1} MB");

        GC.KeepAlive(pageWords);
    }

    private static void SavePageSnapshot(ILogger logger, PdfImageSamples samples, string outputDirectory, int pageNumber)
    {
        string outputPath = Path.Combine(outputDirectory, $"{pageNumber}.png");

        PlaceOverWhite(samples);
        File.WriteAllBytes(outputPath, PdfImageSamplesPngEncoder.Encode(samples));

        logger.LogInformation("Exported page {PageNumber} to {OutputPath}", pageNumber, outputPath);
    }

    // The surface is premultiplied RGBA over nothing; the page is shown over white paper: C = P + (1 − α)·1.
    private static void PlaceOverWhite(PdfImageSamples samples)
    {
        for (int row = 0; row < samples.Height; row++)
        {
            Span<byte> pixels = samples.GetRow(row);

            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                int transparency = byte.MaxValue - pixels[offset + 3];

                pixels[offset] = (byte)(pixels[offset] + transparency);
                pixels[offset + 1] = (byte)(pixels[offset + 1] + transparency);
                pixels[offset + 2] = (byte)(pixels[offset + 2] + transparency);
                pixels[offset + 3] = byte.MaxValue;
            }
        }
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

    private static int CountCharacters(PdfWord[] words)
    {
        int count = 0;

        foreach (PdfWord word in words)
        {
            foreach (PdfWordPart part in word.Parts)
            {
                count += part.Characters.Length;
            }
        }

        return count;
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

using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Fonts.Management;
using PdfPixel.Models;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.Diagnostics;

internal sealed class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PdfPixel.Diagnostics <path-to-pdf>");
            return;
        }

        string pdfPath = args[0];

        // Multiplies the page size before rendering; use a value above 1 for a sharper output image.
        float scale = 1f;

        // PdfDocumentReader needs a logger factory for diagnostics during parsing and rendering.
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = loggerFactory.CreateLogger<Program>();

        // ...and a font provider, used to substitute system fonts for fonts not embedded in the PDF.
        ISkiaFontProvider fontProvider = new WindowsSkiaFontProvider();

        // PdfDocumentReader is the entry point for parsing PDF files.
        PdfDocumentReader reader = new(loggerFactory, fontProvider);

        // The reader needs a seekable, readable stream; it reads the whole document into memory.
        using FileStream fileStream = File.OpenRead(pdfPath);

        // Parses the document and returns the page graph used for rendering.
        using IPdfDocument document = reader.Read(fileStream);

        // Each PDF gets its own output subfolder, named after the source file, under pdfs/.
        string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfs", Path.GetFileNameWithoutExtension(pdfPath));
        Directory.CreateDirectory(outputDirectory);

        // Tracks the total time spent exporting all pages, logged once the export finishes.
        Stopwatch exportStopwatch = Stopwatch.StartNew();

        // Render every page in the document.
        foreach (IPdfPage page in document.Pages)
        {
            // Guards the page's lazily-parsed content stream against concurrent access; use a private
            // object per concurrent render of the same page.
            object contentLocker = new();

            // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
            IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

            // Pass 1: record the page's drawing commands without executing them. No canvas exists
            // yet at this point; PdfCommandRecorder just collects the commands for later replay.
            using PdfCommandRecorder recorder = new(loggerFactory.CreateLogger<PdfCommandRecorder>());

            // Save the execution context's state before applying the page transform, so it can be restored afterwards.
            recorder.Process(SaveStateCommand.Instance);

            // Scales the whole page up or down to the requested output resolution.
            recorder.Process(new ConcatMatrixCommand(SKMatrix.CreateScale(scale, scale)));

            // PDF content is authored with the origin at the bottom-left and Y increasing upward.
            // The canvas has the origin at the top-left and Y increasing downward, so the page must
            // be translated and flipped vertically to land right-side-up in the output image.
            recorder.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(-page.CropBox.Left, page.CropBox.Height + page.CropBox.Top)));
            recorder.Process(new ConcatMatrixCommand(SKMatrix.CreateScale(1, -1)));

            // Records the page content: paths, text, images, and shadings.
            page.Render(recorder, new PdfRenderingParameters(), executionObserver);

            // Annotations (comments, stamps, links, etc.) are recorded separately from page content.
            foreach (PdfPageAnnotation annotation in page.Annotations)
            {
                // Skip annotations excluded from on-screen and print rendering.
                if ((annotation.Content.Flags & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0)
                {
                    continue;
                }

                annotation.Render(recorder, PdfAnnotationVisualStateKind.Normal, new PdfRenderingParameters(), executionObserver);
            }

            // Undoes the scale/translate/flip applied above, leaving the execution context in its original state.
            recorder.Process(RestoreStateCommand.Instance);

            // Pass 2: replay the recorded commands against a real canvas.
            // CropBox is the visible page area in PDF units; scale it to get the output image size.
            SKImageInfo imageInfo = new((int)(page.CropBox.Width * scale), (int)(page.CropBox.Height * scale));
            using SKSurface surface = SKSurface.Create(imageInfo);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            // Bundles the canvas, rendering options, and the objects above into the state every
            // drawing command reads from while the recording is replayed.
            using PdfCommandExecutionContext executionContext = new(
                new PdfCommandExecutionParameters
                {
                    ScaleFactor = scale,
                },
                contentLocker,
                document.OptionalContentGroups,
                executionObserver,
                canvas);

            recorder.Replay(executionContext);

            string outputPath = Path.Combine(outputDirectory, $"{page.PageNumber}.png");
            using SKImage image = surface.Snapshot();
            using SKData pngData = image.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream output = File.Create(outputPath);
            pngData.SaveTo(output);

            logger.LogInformation("Exported page {PageNumber} to {OutputPath}", page.PageNumber, outputPath);
        }

        exportStopwatch.Stop();
        logger.LogInformation("Exported {PageCount} page(s) in {ElapsedMilliseconds} ms", document.Pages.Count, exportStopwatch.ElapsedMilliseconds);
    }
}

using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Encoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap;
using System.Diagnostics;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Execution;
using PdfPixel.OptionalContent;

namespace PdfPixel.Demo.Cli;

internal sealed class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PdfPixel.Demo.Cli <pdf path>");
            return;
        }

        // Path of the PDF to render, the first command-line argument.
        string pdfPath = args[0];

        // Multiplies the page size before rendering; use a value above 1 for a sharper output image.
        float scale = 1f;

        // PdfDocumentReader needs a logger factory for diagnostics during parsing and rendering.
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = loggerFactory.CreateLogger<Program>();

        // ...and a font substitutor, used to substitute system fonts for fonts not embedded in the PDF.
        PdfFontSubstitutor fontSubstitutor = PdfFontSubstitutorFactory.Create(loggerFactory);

        // PdfDocumentReader is the entry point for parsing PDF files.
        PdfDocumentReader reader = new(loggerFactory, fontSubstitutor);

        // The reader needs a seekable, readable stream; it reads the whole document into memory.
        using FileStream fileStream = File.OpenRead(pdfPath);

        // Parses the document and returns the page graph used for rendering.
        using IPdfDocument document = reader.Read(fileStream);

        // Optional content groups are the PDF's layers (e.g. "Notes", "Watermark"). The default
        // states render every layer in its default visibility state.
        PdfOptionalContentStates optionalContentStates = document.OptionalContentProperties.GetDefaultStates();

        // Holds the values command execution reuses across every page of the document.
        using CommandCache commandCache = new();

        // Snap blends in DeviceRGB, reuses its working memory through the pool, and keeps decoded image tiles in
        // the cache, across every page.
        PdfSnapMemoryPool memoryPool = new();
        PdfSnapImageCache imageCache = new();
        PdfSnapSurfaceParameters surfaceParameters = new(PdfDeviceRgbColorSpaceConverter.Instance, memoryPool, imageCache);

        // The pixels of each page: 8-bit red, green, blue and alpha, premultiplied.
        PdfImageChannel[] rgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];

        // White paper under each page, so the images are opaque; the page group composites over it.
        PdfRgbaColor paperColor = new(1f, 1f, 1f, 1f);
        PdfPaint paperPaint = new(PdfPaintStyle.Fill, paperColor);

        // Each PDF gets its own output subfolder, named after the source file, under pdfs/.
        string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfs", Path.GetFileNameWithoutExtension(pdfPath));
        Directory.CreateDirectory(outputDirectory);

        // Tracks the total time spent exporting all pages, logged once the export finishes.
        Stopwatch exportStopwatch = Stopwatch.StartNew();

        // Render every page in the document.
        foreach (IPdfPage page in document.Pages)
        {
            // CropBox is the visible page area in PDF units; rotating and scaling it gives the
            // output image size, with width and height swapped when /Rotate is a quarter turn.
            PdfSize outputSize = page.CropBox.GetTransformedSize(page.Rotation, scale);
            int width = (int)outputSize.Width;
            int height = (int)outputSize.Height;
            PdfImageSamples pixels = new(width, height, PdfImageSampleFormat.Integer, 8, rgbaChannels, isPremultiplied: true);

            // Guards the page's lazily-parsed content stream against concurrent access; use a private
            // object per concurrent render of the same page.
            object contentLocker = new();

            // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
            IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

            // Bundles the rendering options and the objects above into the state every drawing command
            // reads from while the page is replayed.
            PdfCommandExecutionContext executionContext = new(
                commandCache,
                new PdfCommandExecutionParameters(),
                contentLocker,
                optionalContentStates,
                PdfAnnotationStates.Empty,
                executionObserver);

            // Executes each drawing command immediately onto the pixels.
            using PdfSnapSurface processor = new(pixels, surfaceParameters, executionContext, loggerFactory.CreateLogger<PdfSnapSurface>());

            // Fills the whole image with the paper.
            PdfPathBuilder paperArea = new();
            paperArea.AddRect(new PdfRectangle(0, 0, width, height));
            DrawPathCommand drawPaper = new(paperArea.ToPath(), paperPaint);
            processor.Process(drawPaper);

            // Save the execution context's state before applying the page transform, so it can be restored afterwards.
            processor.Process(SaveStateCommand.Instance);

            // Clips to the crop box, flips the page onto the image's top-left origin, applies the
            // page's own /Rotate, and scales it to the requested output resolution.
            processor.ApplyPageTransformations(page, scale: scale);

            // Records everything the page group holds, so it can be composited as one group.
            PdfCommandRecorder pageContent = new();

            // Draws the page content: paths, text, images, and shadings.
            page.Render(pageContent, new PdfRenderingParameters(), executionObserver);

            // Annotations (comments, stamps, links, etc.) are rendered separately from page content.
            foreach (PdfPageAnnotation annotation in page.GetAnnotations())
            {
                // Skip annotations excluded from on-screen and print rendering.
                if ((annotation.Content.Flags & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0)
                {
                    continue;
                }

                annotation.Render(pageContent, PdfAnnotationVisualStateKind.Normal, new PdfRenderingParameters(), executionObserver);
            }

            // Draws the page content and the annotations over it as the page group.
            processor.Process(page.CreatePageGroup(new DrawRecordingCommand(pageContent)));

            // Undoes the scale/translate/flip applied above, leaving the execution context in its original state.
            processor.Process(RestoreStateCommand.Instance);

            // Writes the pixels as a PNG file; opaque pixels are stored without alpha.
            string outputPath = Path.Combine(outputDirectory, $"{page.PageNumber}.png");
            byte[] png = PdfImageSamplesPngEncoder.Encode(pixels);
            File.WriteAllBytes(outputPath, png);

            logger.LogInformation("Exported page {PageNumber} to {OutputPath}", page.PageNumber, outputPath);
        }

        exportStopwatch.Stop();
        logger.LogInformation("Exported {PageCount} page(s) in {ElapsedMilliseconds} ms", document.Pages.Count, exportStopwatch.ElapsedMilliseconds);
    }
}

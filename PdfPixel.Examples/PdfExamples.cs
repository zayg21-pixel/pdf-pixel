using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Encoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Snap;

namespace PdfPixel.Examples;

/// <summary>
/// Rendering a PDF page to a raster image: at different scales, cropped to a region of the
/// page, and rotated.
/// </summary>
internal static class PdfExamples
{
    private const string Format = "Pdf";
    private const string SourceFile = "hello-world.pdf";

    /// <summary>
    /// Runs every PDF example.
    /// </summary>
    public static void Run()
    {
        // Scale multiplies the page size before rendering; above 1 gives a sharper output image.
        RenderPage("the whole page at scale 1", "page-scale-1.png", scale: 1f, userRotation: 0, region: null);
        RenderPage("the whole page at scale 3", "page-scale-3.png", scale: 3f, userRotation: 0, region: null);

        // A region is given in PDF units measured from the bottom-left corner of the page, as the
        // two corners (minimum x, minimum y, maximum x, maximum y). This one covers the swatch strip
        // and the top half of the photo: only the image tiles inside the region are decoded.
        PdfRectangle swatchesAndPhoto = new(40f, 148f, 555f, 340f);
        RenderPage("one region of the page at scale 2", "page-region.png", scale: 2f, userRotation: 0, swatchesAndPhoto);

        // Rotation is clockwise and must be a multiple of 90; it adds to the page's own /Rotate.
        RenderPage("the whole page turned 90 degrees", "page-rotated-90.png", scale: 1f, userRotation: 90, region: null);
        RenderPage("the whole page turned 180 degrees", "page-rotated-180.png", scale: 1f, userRotation: 180, region: null);
    }

    /// <summary>
    /// Renders the first page of the source document to a PNG.
    /// </summary>
    /// <param name="description">Text naming this variant on the console.</param>
    /// <param name="outputFileName">Name of the PNG written to the output folder.</param>
    /// <param name="scale">Factor applied to the rendered area.</param>
    /// <param name="userRotation">Clockwise rotation in degrees, added to the page's own rotation.</param>
    /// <param name="region">Area of the page to render, or null for the whole crop box.</param>
    private static void RenderPage(string description, string outputFileName, float scale, int userRotation, PdfRectangle? region)
    {
        Console.WriteLine($"[Pdf] Rendering {description} of {SourceFile}...");

        // PdfDocumentReader needs a logger factory for diagnostics during parsing and rendering...
        ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

        // ...and a font substitutor, which supplies system fonts for fonts not embedded in the PDF.
        PdfFontSubstitutor fontSubstitutor = PdfFontSubstitutorFactory.Create(loggerFactory);
        PdfDocumentReader reader = new(loggerFactory, fontSubstitutor);

        // The reader parses lazily from the stream it is handed, so give it the whole file in memory.
        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, SourceFile));
        using MemoryStream pdfStream = new(fileBytes);
        using IPdfDocument document = reader.Read(pdfStream);
        IPdfPage page = document.Pages[0];

        // CropBox is the visible page area in PDF units, and is what a null region falls back to.
        PdfRectangle pageBox = region ?? page.CropBox;

        // A page whose /Rotate is not a quarter turn is displayed unrotated.
        int pageRotation = (page.Rotation % 90 == 0) ? page.Rotation : 0;
        int rotation = pageRotation + userRotation;

        // Rotating and scaling the box gives the output image size, with width and height swapped
        // when the rotation is a quarter turn.
        PdfSize outputSize = pageBox.GetTransformedSize(rotation, scale);
        int width = (int)outputSize.Width;
        int height = (int)outputSize.Height;

        // The pixels the page is drawn onto: 8-bit red, green, blue and alpha, premultiplied.
        PdfImageChannel[] rgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];
        PdfImageSamples pixels = new(width, height, PdfImageSampleFormat.Integer, 8, rgbaChannels, isPremultiplied: true);

        // Held while each drawing command executes; share one object per document with any other
        // code that reads the same document from another thread.
        object contentLocker = new();

        // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        // Optional content groups are the PDF's layers (e.g. "Notes", "Watermark"). The default
        // states render every layer in its default visibility state.
        PdfOptionalContentStates optionalContentStates = document.OptionalContentProperties.GetDefaultStates();

        // Holds the values drawing commands reuse; share one per document across its renders.
        using CommandCache commandCache = new();

        // Bundles the rendering options and the objects above into the state every drawing command
        // reads from while the page is replayed.
        PdfCommandExecutionParameters executionParameters = new();
        PdfCommandExecutionContext executionContext = new(
            commandCache,
            executionParameters,
            contentLocker,
            optionalContentStates,
            PdfAnnotationStates.Empty,
            executionObserver);

        // Snap blends in DeviceRGB, reuses its working memory through the pool, and keeps decoded
        // image tiles in the cache.
        PdfSnapMemoryPool memoryPool = new();
        PdfSnapImageCache imageCache = new();
        PdfSnapSurfaceParameters surfaceParameters = new(PdfDeviceRgbColorSpaceConverter.Instance, memoryPool, imageCache);

        // Executes each drawing command immediately onto the pixels.
        using PdfSnapSurface surface = new(pixels, surfaceParameters, executionContext, NullLogger<PdfSnapSurface>.Instance);

        // White paper under the page, so the image is opaque; the page group composites over it.
        PdfPathBuilder paperArea = new();
        paperArea.AddRect(new PdfRectangle(0, 0, width, height));
        PdfRgbaColor paperColor = new(1f, 1f, 1f, 1f);
        PdfPaint paperPaint = new(PdfPaintStyle.Fill, paperColor);
        DrawPathCommand drawPaper = new(paperArea.ToPath(), paperPaint);
        surface.Process(drawPaper);

        // Saves the surface's drawing state before the page transform, to restore it afterwards.
        surface.Process(SaveStateCommand.Instance);

        try
        {
            // Clips to the box, flips the page onto the image's top-left origin, applies the
            // rotation, and scales to the requested output resolution.
            surface.ApplyPageTransformations(pageBox, rotation, scale);

            // Records the page content: paths, text, images, and shadings.
            PdfRenderingParameters renderingParameters = new();
            PdfCommandRecorder pageContent = new();
            page.Render(pageContent, renderingParameters, executionObserver);

            // Draws the recorded content as the page group, blended in the page's own color space.
            DrawRecordingCommand drawPageContent = new(pageContent);
            surface.Process(page.CreatePageGroup(drawPageContent));
        }
        finally
        {
            surface.Process(RestoreStateCommand.Instance);
        }

        // Writes the pixels as a PNG file; opaque pixels are stored without alpha.
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        byte[] png = PdfImageSamplesPngEncoder.Encode(pixels);
        File.WriteAllBytes(outputPath, png);

        Console.WriteLine($"[Pdf]   {width}x{height} -> {ExamplePaths.Relative(outputPath)}");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Commands;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Skia;
using PdfPixel.Skia.Fonts;
using SkiaSharp;

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
        // two corners (minimum x, minimum y, maximum x, maximum y). This one covers the shape row.
        PdfRectangle shapeRow = new(40f, 450f, 555f, 640f);
        RenderPage("one region of the page at scale 2", "page-region.png", scale: 2f, userRotation: 0, shapeRow);

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
        SkiaFontSubstitutor fontSubstitutor = new(loggerFactory);
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
        SKImageInfo imageInfo = new((int)outputSize.Width, (int)outputSize.Height);
        using SKSurface surface = SKSurface.Create(imageInfo);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Guards the page's lazily-parsed content stream against concurrent access; use a private
        // object per concurrent render of the same page.
        object contentLocker = new();

        // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        // Optional content groups are the PDF's layers (e.g. "Notes", "Watermark"). Passing the
        // document's groups renders every layer in its default visibility state.
        IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> optionalContentGroups = document.OptionalContentGroups;

        // Bundles the canvas, rendering options, and the objects above into the state every
        // drawing command reads from while the page is replayed.
        PdfCommandExecutionParameters executionParameters = new();
        using PdfCommandExecutionContext executionContext = new(
            document,
            executionParameters,
            contentLocker,
            optionalContentGroups,
            executionObserver);

        // Executes each drawing command immediately against the canvas.
        SkCanvasCommandProcessor processor = new(canvas, executionContext, NullLogger<SkCanvasCommandProcessor>.Instance);

        // Saves canvas and execution state before the page transform, to restore both afterwards.
        processor.Process(SaveStateCommand.Instance);

        try
        {
            // Clips to the box, flips the page onto the canvas' top-left origin, applies the
            // rotation, and scales to the requested output resolution.
            processor.ApplyPageTransformations(pageBox, rotation, scale);

            // Draws the page content: paths, text, images, and shadings.
            PdfRenderingParameters renderingParameters = new();
            page.Render(processor, renderingParameters, executionObserver);
        }
        finally
        {
            processor.Process(RestoreStateCommand.Instance);
        }

        // The rendered page is already a Skia image, so Skia's own PNG encoder writes it out.
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(outputPath);
        encoded.SaveTo(output);

        Console.WriteLine($"[Pdf]   {imageInfo.Width}x{imageInfo.Height} -> {ExamplePaths.Relative(outputPath)}");
    }
}

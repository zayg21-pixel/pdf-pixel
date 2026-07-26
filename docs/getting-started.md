# Getting Started

## Install

```
dotnet add package PdfPixel
```

## Render every page to PNG, with annotations

```csharp
using Microsoft.Extensions.Logging;
using PdfPixel;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Fonts.Management;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading;

// The only thing you need to change to run this example.
string pdfPath = "sample.pdf";

// Multiplies the page size before rendering; use a value above 1 for a sharper output image.
float scale = 2f;

// PdfDocumentReader needs a logger factory for diagnostics during parsing and rendering.
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// ...and a font provider, used to substitute system fonts for fonts not embedded in the PDF.
IFontProvider fontProvider = new WindowsFontProvider(loggerFactory);

// PdfDocumentReader is the entry point for parsing PDF files.
PdfDocumentReader reader = new(loggerFactory, fontProvider);

// The reader needs a seekable, readable stream; it reads the whole document into memory.
using FileStream fileStream = File.OpenRead(pdfPath);

// Parses the document and returns the page graph used for rendering.
using IPdfDocument document = reader.Read(fileStream);

// Optional content groups are the PDF's layers (e.g. "Notes", "Watermark"). Passing the
// document's groups renders every layer in its default visibility state.
IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> optionalContentGroups = document.OptionalContentGroups;

// Render every page in the document.
foreach (IPdfPage page in document.Pages)
{
    // CropBox is the visible page area in PDF units; scale it to get the output image size.
    SKImageInfo imageInfo = new((int)(page.CropBox.Width * scale), (int)(page.CropBox.Height * scale));
    using SKSurface surface = SKSurface.Create(imageInfo);
    SKCanvas canvas = surface.Canvas;
    canvas.Clear(SKColors.White);

    // Guards the page's lazily-parsed content stream against concurrent access; use a private
    // object per concurrent render of the same page.
    object contentLocker = new();

    // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
    IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

    // Bundles the canvas, rendering options, and the objects above into the state every
    // drawing command reads from while the page is replayed.
    using PdfCommandExecutionContext executionContext = new(
        new PdfCommandExecutionParameters
        {
            ScaleFactor = scale
        },
        contentLocker,
        optionalContentGroups,
        executionObserver,
        canvas);

    // Executes each drawing command immediately against executionContext.Canvas.
    using SkCanvasCommandProcessor processor = new(executionContext, loggerFactory.CreateLogger<SkCanvasCommandProcessor>());

    // Save the execution context's state before applying the page transform, so it can be restored afterwards.
    processor.Process(SaveStateCommand.Instance);

    // Scales the whole page up or down to the requested output resolution.
    processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateScale(scale, scale)));

    // PDF content is authored with the origin at the bottom-left and Y increasing upward.
    // The canvas has the origin at the top-left and Y increasing downward, so the page must
    // be translated and flipped vertically to land right-side-up in the output image.
    processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateTranslation(-page.CropBox.Left, page.CropBox.Height + page.CropBox.Top)));
    processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateScale(1, -1)));

    // Draws the page content: paths, text, images, and shadings.
    page.Render(processor, new PdfRenderingParameters(), executionObserver);

    // Annotations (comments, stamps, links, etc.) are rendered separately from page content.
    foreach (PdfPageAnnotation annotation in page.Annotations)
    {
        // Skip annotations excluded from on-screen and print rendering.
        if ((annotation.Content.Flags & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0)
        {
            continue;
        }

        annotation.Render(processor, PdfAnnotationVisualStateKind.Normal, new PdfRenderingParameters(), executionObserver);
    }

    // Undoes the scale/translate/flip applied above, leaving the execution context in its original state.
    processor.Process(RestoreStateCommand.Instance);

    // Encode the finished surface to PNG and write it next to the source PDF, named by page number.
    string outputPath = $"{Path.GetFileNameWithoutExtension(pdfPath)}-page-{page.PageNumber}.png";
    using SKImage image = surface.Snapshot();
    using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
    using FileStream output = File.OpenWrite(outputPath);
    png.SaveTo(output);
}
```

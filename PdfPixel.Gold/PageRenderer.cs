using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Commands.Skia;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Gold;

/// <summary>
/// Renders PDF pages the same way the console demo does, and reads and writes the PNGs the
/// generator and the comparer work with.
/// </summary>
internal static class PageRenderer
{
    private const int BytesPerPixel = 4;

    // Snapshots are only comparable when every run renders at the same size, so the page size is
    // used as-is.
    private const float Scale = 1f;

    /// <summary>
    /// Renders every page of the document. The caller disposes the returned bitmaps.
    /// </summary>
    public static List<SKBitmap> RenderPages(PdfDocumentReader reader, string pdfPath)
    {
        // The reader needs a seekable, readable stream; it reads the whole document into memory.
        using FileStream fileStream = File.OpenRead(pdfPath);
        using IPdfDocument document = reader.Read(fileStream);

        List<SKBitmap> pages = new();

        foreach (IPdfPage page in document.Pages)
        {
            // Optional content groups are the PDF's layers; passing the document's groups renders
            // every layer in its default visibility state.
            pages.Add(RenderPage(page, document.OptionalContentGroups));
        }

        return pages;
    }

    /// <summary>
    /// Writes a rendered page as a PNG, replacing any file already there.
    /// </summary>
    public static void SavePng(SKBitmap bitmap, string path)
    {
        using SKImage image = SKImage.FromPixelCopy(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes);
        using SKData pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(path);
        pngData.SaveTo(output);
    }

    /// <summary>
    /// Reads a PNG written by an earlier run.
    /// </summary>
    public static SKBitmap LoadPng(string path)
    {
        using SKImage image = SKImage.FromEncodedData(path);

        if (image == null)
        {
            throw new InvalidDataException($"{path} could not be decoded.");
        }

        return ReadPixels(image);
    }

    /// <summary>
    /// Number of pixels that differ between two pages of the same size.
    /// </summary>
    public static int CountDifferentPixels(SKBitmap golden, SKBitmap rendered)
    {
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();
        ReadOnlySpan<byte> renderedPixels = rendered.GetPixelSpan();

        if (goldenPixels.SequenceEqual(renderedPixels))
        {
            return 0;
        }

        int differentPixels = 0;

        for (int offset = 0; offset + BytesPerPixel <= goldenPixels.Length; offset += BytesPerPixel)
        {
            if (!goldenPixels.Slice(offset, BytesPerPixel).SequenceEqual(renderedPixels.Slice(offset, BytesPerPixel)))
            {
                differentPixels++;
            }
        }

        return differentPixels;
    }

    private static SKBitmap RenderPage(IPdfPage page, IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> optionalContentGroups)
    {
        // CropBox is the visible page area in PDF units; scale it to get the output image size.
        int width = (int)(page.CropBox.Width * Scale);
        int height = (int)(page.CropBox.Height * Scale);

        if (width < 1 || height < 1)
        {
            throw new InvalidOperationException($"page {page.PageNumber} has an empty crop box: {page.CropBox.Width} x {page.CropBox.Height}.");
        }

        SKImageInfo imageInfo = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(imageInfo);

        if (surface == null)
        {
            throw new InvalidOperationException($"page {page.PageNumber} needs a {width} x {height} surface, which could not be created.");
        }

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Guards the page's lazily-parsed content stream against concurrent access; use a private
        // object per concurrent render of the same page.
        object contentLocker = new();

        // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        PdfCommandExecutionParameters executionParameters = new()
        {
            ScaleFactor = Scale,
        };

        // Bundles the canvas, rendering options, and the objects above into the state every
        // drawing command reads from while the page is replayed.
        using PdfCommandExecutionContext executionContext = new(
            executionParameters,
            contentLocker,
            optionalContentGroups,
            executionObserver);

        // Executes each drawing command immediately against canvas.
        SkCanvasCommandProcessor processor = new(canvas, executionContext, NullLogger<SkCanvasCommandProcessor>.Instance);

        // Save the execution context's state before applying the page transform, so it can be restored afterwards.
        processor.Process(SaveStateCommand.Instance);

        // Scales the whole page up or down to the requested output resolution.
        processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateScale(Scale, Scale)));

        // PDF content is authored with the origin at the bottom-left and Y increasing upward, while
        // the canvas has the origin at the top-left and Y increasing downward, so the page must be
        // translated and flipped vertically to land right-side-up in the output image.
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

        canvas.Flush();

        using SKImage image = surface.Snapshot();

        return ReadPixels(image);
    }

    // Straight (unpremultiplied) RGBA is the layout a PNG stores, so a page written to disk and read
    // back gives the exact same bytes, which is what a pixel-perfect comparison needs.
    private static SKBitmap ReadPixels(SKImage image)
    {
        SKImageInfo imageInfo = new(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap bitmap = new(imageInfo);

        if (!image.ReadPixels(imageInfo, bitmap.GetPixels(), imageInfo.RowBytes, 0, 0))
        {
            bitmap.Dispose();

            throw new InvalidOperationException("pixels could not be read back.");
        }

        return bitmap;
    }
}

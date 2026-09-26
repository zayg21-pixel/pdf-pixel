using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Fonts.Management;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Skia;
using PdfPixel.Skia.Fonts;
using SkiaSharp;

namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Opens corpus PDFs and renders their pages the same way the console demo does.
/// </summary>
internal static class PdfUtilities
{
    // Snapshots are only comparable when every run renders at the same size, so the page size is
    // used as-is.
    private const float Scale = 1f;

    /// <summary>
    /// Creates the reader every command renders with.
    /// </summary>
    public static PdfDocumentReader CreateReader()
    {
        // A font substitutor is used to substitute system fonts for fonts not embedded in the PDF, and
        // PdfDocumentReader is the entry point for parsing PDF files.
        SkiaFontSubstitutor fontSubstitutor = new(NullLoggerFactory.Instance);

        return new(NullLoggerFactory.Instance, fontSubstitutor);
    }

    /// <summary>
    /// Renders the requested pages of the document one at a time, or every page when no page number
    /// is given. An encrypted document needs its user password.
    /// </summary>
    /// <remarks>
    /// A page is rendered only as the caller asks for it, so that a long document never holds more
    /// than one page bitmap at a time. The caller disposes each bitmap before taking the next one.
    /// </remarks>
    public static IEnumerable<(int PageNumber, SKBitmap Bitmap)> RenderPages(PdfDocumentReader reader, string pdfPath, IReadOnlyList<int> pageNumbers, string? password)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"{pdfPath} is not downloaded; run setup first.", pdfPath);
        }

        // The reader parses lazily from the stream it is given, so the whole document is read into
        // memory first and never touches the disk again while it renders.
        using MemoryStream documentStream = new(File.ReadAllBytes(pdfPath));
        using IPdfDocument document = reader.Read(documentStream, (reason, authEvent) => (reason == PdfPasswordRequestReason.PasswordRequired) ? password : null);

        // Checked before anything is rendered, so that a bad page number costs nothing and leaves
        // no half-written output behind.
        foreach (int requestedPage in pageNumbers)
        {
            if (requestedPage > document.Pages.Count)
            {
                throw new InvalidOperationException($"the document has {document.Pages.Count} page(s), which does not cover the requested page {requestedPage}.");
            }
        }

        foreach (IPdfPage page in document.Pages)
        {
            if (pageNumbers.Count > 0 && !pageNumbers.Contains(page.PageNumber))
            {
                continue;
            }

            // Optional content groups are the PDF's layers; passing the document's groups renders
            // every layer in its default visibility state.
            yield return (page.PageNumber, RenderPage(document, page, document.OptionalContentGroups));
        }
    }

    /// <summary>
    /// Releases the resources and fonts Skia cached while the previous PDF rendered.
    /// </summary>
    public static void PurgeSkiaCaches()
    {
        SKGraphics.PurgeResourceCache();
        SKGraphics.PurgeFontCache();
    }

    private static SKBitmap RenderPage(IPdfDocument document, IPdfPage page, IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> optionalContentGroups)
    {
        // CropBox is the visible page area in PDF units; scale it to get the output image size.
        int width = (int)(page.CropBox.Width * Scale);
        int height = (int)(page.CropBox.Height * Scale);

        if (width < 1 || height < 1)
        {
            throw new InvalidOperationException($"page {page.PageNumber} has an empty crop box: {page.CropBox.Width} x {page.CropBox.Height}.");
        }

        SKImageInfo imageInfo = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        SKBitmap bitmap = new(imageInfo);

        // The page is drawn straight into the bitmap's pixels, so it is held once and handed back
        // without a copy.
        using SKSurface surface = SKSurface.Create(imageInfo, bitmap.GetPixels(), bitmap.RowBytes);

        if (surface == null)
        {
            bitmap.Dispose();

            throw new InvalidOperationException($"page {page.PageNumber} needs a {width} x {height} surface, which could not be created.");
        }

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Guards the page's lazily-parsed content stream against concurrent access; use a private
        // object per concurrent render of the same page.
        object contentLocker = new();

        // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        PdfCommandExecutionParameters executionParameters = new();

        // Bundles the canvas, rendering options, and the objects above into the state every
        // drawing command reads from while the page is replayed.
        using PdfCommandExecutionContext executionContext = new(
            document,
            executionParameters,
            contentLocker,
            optionalContentGroups,
            executionObserver);

        // Executes each drawing command immediately against canvas.
        SkCanvasCommandProcessor processor = new(canvas, executionContext, NullLogger<SkCanvasCommandProcessor>.Instance);

        // Save the execution context's state before applying the page transform, so it can be restored afterwards.
        processor.Process(SaveStateCommand.Instance);

        // PDF content is authored with the origin at the bottom-left and Y increasing upward, while
        // the canvas has the origin at the top-left and Y increasing downward, so the page must be
        // translated and flipped vertically to land right-side-up in the output image.
        processor.ApplyPageTransformations(page.CropBox, scale: Scale, clipToBounds: false);

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

        return bitmap;
    }
}

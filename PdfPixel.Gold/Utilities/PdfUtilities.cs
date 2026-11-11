using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Execution;
using PdfPixel.Encryption;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.Snap;
using PdfPixel.Transparency;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Opens corpus PDFs and renders their pages the same way the console demo does.
/// </summary>
internal static class PdfUtilities
{
    // Snapshots are only comparable when every run renders at the same size, so the page size is
    // used as-is.
    private const float Scale = 1f;

    // Each output sample is one byte, as the PNG snapshots store them.
    private const int OutputBitsPerComponent = 8;

    // A pixel read as a little-endian 32-bit value holds red in its lowest byte and alpha in its highest.
    private const int AlphaShift = 24;
    private const uint OpaqueValue = 255;
    private const uint OpaqueAlpha = 0xFF000000u;
    private const uint ComponentSpread = 0x00010101u;

    /// <summary>
    /// Creates the reader every command renders with.
    /// </summary>
    public static PdfDocumentReader CreateReader()
    {
        // A font substitutor is used to substitute system fonts for fonts not embedded in the PDF, and
        // PdfDocumentReader is the entry point for parsing PDF files.
        PdfFontSubstitutor fontSubstitutor = PdfFontSubstitutorFactory.Create(NullLoggerFactory.Instance);

        return new(NullLoggerFactory.Instance, fontSubstitutor);
    }

    /// <summary>
    /// Renders the requested pages of the document one at a time, or every page when no page number
    /// is given, with the Snap rasterizer, as opaque 8-bit RGBA samples. An encrypted document needs
    /// its user password.
    /// </summary>
    public static IEnumerable<(int PageNumber, PdfImageSamples Samples)> RenderPages(PdfDocumentReader reader, string pdfPath, IReadOnlyList<int> pageNumbers, string? password)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"{pdfPath} is not downloaded; run setup first.", pdfPath);
        }

        // The reader parses lazily from the stream it is given, so the whole document is read into
        // memory first and never touches the disk again while it renders.
        using MemoryStream documentStream = new(File.ReadAllBytes(pdfPath));
        using IPdfDocument document = reader.Read(documentStream, request => (request.Reason == PdfCredentialRequestReason.CredentialRequired && password != null) ? new PdfPasswordCredential(password) : null);

        // Checked before anything is rendered, so that a bad page number costs nothing and leaves
        // no half-written output behind.
        foreach (int requestedPage in pageNumbers)
        {
            if (requestedPage > document.Pages.Count)
            {
                throw new InvalidOperationException($"the document has {document.Pages.Count} page(s), which does not cover the requested page {requestedPage}.");
            }
        }

        // Holds the values command execution reuses across every page of the document.
        using CommandCache commandCache = new();

        foreach (IPdfPage page in document.Pages)
        {
            if (pageNumbers.Count > 0 && !pageNumbers.Contains(page.PageNumber))
            {
                continue;
            }

            // Optional content groups are the PDF's layers; the default states render every layer in its
            // default visibility state.
            yield return (page.PageNumber, RenderPage(commandCache, page, document.OptionalContentProperties.GetDefaultStates()));
        }
    }

    private static PdfImageSamples RenderPage(CommandCache commandCache, IPdfPage page, PdfOptionalContentStates optionalContentStates)
    {
        // CropBox is the visible page area in PDF units; scale it to get the output image size.
        int width = (int)(page.CropBox.Width * Scale);
        int height = (int)(page.CropBox.Height * Scale);

        if (width < 1 || height < 1)
        {
            throw new InvalidOperationException($"page {page.PageNumber} has an empty crop box: {page.CropBox.Width} x {page.CropBox.Height}.");
        }

        // Guards the document's content streams against concurrent access; use a private
        // object per concurrent render of the same page.
        object contentLocker = new();

        // Lets a long-running render be cancelled cooperatively; CancellationToken.None never cancels.
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        PdfCommandExecutionParameters executionParameters = new() { DeviceProfiles = PdfDeviceProfiles.FromPage(page) };

        // Bundles the rendering options and the objects above into the state every drawing command
        // reads from while the page is replayed.
        PdfCommandExecutionContext executionContext = new(
            commandCache,
            executionParameters,
            contentLocker,
            optionalContentStates,
            PdfAnnotationStates.Empty,
            executionObserver);

        return RenderWithSnap(page, width, height, executionContext, executionObserver);
    }

    private static PdfImageSamples RenderWithSnap(IPdfPage page, int width, int height, PdfCommandExecutionContext executionContext, IPdfExecutionObserver executionObserver)
    {
        PdfSnapImageCache imageCache = new(tileSize: 256, putCapacityMegabytes: 0, rentCapacityMegabytes: 0);
        PdfSnapSurfaceParameters surfaceParameters = new(PdfDeviceRgbColorSpaceConverter.Instance, new PdfSnapMemoryPool(), imageCache);
        // The surface draws premultiplied RGBA over nothing straight onto the samples; the page is shown over white paper.
        PdfImageChannel[] channels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];
        PdfImageSamples samples = new(width, height, PdfImageSampleFormat.Integer, OutputBitsPerComponent, channels, isPremultiplied: true);
        PdfSnapSurface surface = new(samples, surfaceParameters, executionContext, NullLogger<PdfSnapSurface>.Instance);
        DrawPage(surface, page, executionObserver);
        ComposeOverWhite(samples);

        return samples;
    }

    /// <summary>
    /// Composites premultiplied 8-bit RGBA samples over white in place: each color becomes c + 255 − α and alpha 255.
    /// A premultiplied color never exceeds its alpha, so the sum never carries into the next component.
    /// </summary>
    private static void ComposeOverWhite(PdfImageSamples samples)
    {
        Vector<uint> opaqueValues = new(OpaqueValue);
        Vector<uint> componentSpreads = new(ComponentSpread);
        Vector<uint> opaqueAlphas = new(OpaqueAlpha);

        for (int row = 0; row < samples.Height; row++)
        {
            Span<uint> pixels = MemoryMarshal.Cast<byte, uint>(samples.GetRow(row));
            ref uint firstPixel = ref MemoryMarshal.GetReference(pixels);
            int index = 0;

            for (; index <= pixels.Length - Vector<uint>.Count; index += Vector<uint>.Count)
            {
                Vector<uint> values = Vector.LoadUnsafe(ref firstPixel, (nuint)index);
                Vector<uint> backgrounds = (opaqueValues - Vector.ShiftRightLogical(values, AlphaShift)) * componentSpreads;

                ((values + backgrounds) | opaqueAlphas).StoreUnsafe(ref firstPixel, (nuint)index);
            }

            for (; index < pixels.Length; index++)
            {
                ref uint pixel = ref Unsafe.Add(ref firstPixel, index);
                uint background = (OpaqueValue - (pixel >> AlphaShift)) * ComponentSpread;

                pixel = (pixel + background) | OpaqueAlpha;
            }
        }
    }

    private static void DrawPage(IPdfCommandProcessor processor, IPdfPage page, IPdfExecutionObserver executionObserver)
    {
        // Save the execution context's state before applying the page transform, so it can be restored afterwards.
        processor.Process(SaveStateCommand.Instance);

        // PDF content is authored with the origin at the bottom-left and Y increasing upward, while
        // the canvas has the origin at the top-left and Y increasing downward, so the page must be
        // translated and flipped vertically to land right-side-up in the output image.
        processor.ApplyPageTransformations(page.CropBox, scale: Scale, clipToBounds: false);

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

        // Draws the page content and the annotations over it as the page group. The surface's root is already an
        // isolated, transparent group blending in DeviceRGB, so a page group declaring DeviceRGB or no color space and
        // without knockout is drawn straight onto it.
        DrawRecordingCommand content = new(pageContent);
        DrawGroupCommand pageGroup = page.CreatePageGroup(content);
        PdfTransparencyGroupContext pageGroupContext = pageGroup.Source.Group;
        bool blendsLikeRoot = pageGroupContext.ColorSpace == null
            || pageGroupContext.ColorSpace is PdfDeviceRgbColorSpaceConverter;

        if (blendsLikeRoot && !pageGroupContext.Knockout)
        {
            processor.Process(content);
        }
        else
        {
            processor.Process(pageGroup);
        }

        // Undoes the scale/translate/flip applied above, leaving the execution context in its original state.
        processor.Process(RestoreStateCommand.Instance);
    }
}

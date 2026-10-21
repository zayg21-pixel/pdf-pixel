using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Transparency;
using System;

namespace PdfPixel.Rendering.Image;

/// <summary>
/// Standard PDF image renderer supporting normal images, image masks, and soft masks.
/// Delegates rendering to <see cref="PdfImageFillRenderTarget"/> which handles decoding, shader creation, and blending.
/// </summary>
internal class PdfImageRenderer
{
    private readonly PdfRenderer _renderer;
    private readonly ILoggerFactory _factory;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public PdfImageRenderer(PdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Renders a PDF image using the provided command processor and graphics state.
    /// Wraps the image in a soft-mask scope when required and delegates to <see cref="PdfImageFillRenderTarget"/>.
    /// </summary>
    /// <param name="processor">The command processor to draw through.</param>
    /// <param name="pdfImage">The <see cref="PdfImage"/> to be rendered. Must not be <see langword="null"/> and must have positive
    /// dimensions.</param>
    /// <param name="state">The <see cref="PdfGraphicsState"/> that defines the rendering state for the image.</param>
    public void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (pdfImage == null)
        {
            throw new ArgumentNullException(nameof(pdfImage));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
        {
            return;
        }

        if (!state.RenderingParameters.ProcessImages)
        {
            return;
        }

        if (state.RenderingParameters.ExtractContent)
        {
            PdfRectangle unitSquare = new(0, 0, 1, 1);
            processor.Process(new PdfContentCommand<PdfImage>(PdfContentKind.Image, PdfMatrix.Identity, unitSquare, pdfImage));
        }

        if (!state.RenderingParameters.RenderContent)
        {
            return;
        }

        PdfImageFillRenderTarget target = new(pdfImage, state, _factory);

        if (HasOwnMask(pdfImage))
        {
            target.Render(processor);
            return;
        }

        using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, target.Bounds);

        target.Render(processor);
    }

    private static bool HasOwnMask(PdfImage pdfImage)
    {
        return pdfImage.SoftMask != null
            || pdfImage.Mask != null
            || pdfImage.ColorKeyMask != null;
    }
}

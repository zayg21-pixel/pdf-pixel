using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;
using System;

namespace PdfPixel.Rendering.Image;

/// <summary>
/// Standard PDF image renderer supporting normal images, image masks, and soft masks.
/// Delegates rendering to <see cref="ImageFillRenderTarget"/> which handles decoding, shader creation, and blending.
/// </summary>
public class ImageRenderer : IImageRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _factory;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public ImageRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Renders a PDF image using the provided command processor and graphics state.
    /// Wraps the image in a soft-mask scope when required and delegates to <see cref="ImageFillRenderTarget"/>.
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

        if (!state.RenderingParameters.RenderImages)
        {
            return;
        }

        ImageFillRenderTarget target = new(pdfImage, state, _factory);

        if (HasOwnMask(pdfImage))
        {
            target.Render(processor);
            return;
        }

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state, target.Bounds);
        softMaskScope.BeginDrawContent();

        target.Render(processor);
    }

    private static bool HasOwnMask(PdfImage pdfImage)
    {
        return pdfImage.SoftMask != null
            || pdfImage.StencilMask != null
            || pdfImage.MaskArray != null;
    }
}

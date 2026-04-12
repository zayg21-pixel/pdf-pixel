using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;
using System;

namespace PdfPixel.Rendering.Image;

/// <summary>
/// Standard PDF image renderer supporting normal images, image masks, and soft masks.
/// Emits a single <see cref="DrawImageCommand"/> that handles decoding, shader creation,
/// and caching at execution time.
/// </summary>
public class ImageRenderer : IImageRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _factory;

    public ImageRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Renders a PDF image using the provided command processor and graphics state.
    /// Wraps the image in a soft-mask scope when required and emits a single
    /// <see cref="DrawImageCommand"/> that defers all heavy work until replay.
    /// </summary>
    /// <param name="processor">The command processor to draw through.</param>
    /// <param name="pdfImage">The <see cref="PdfImage"/> to be rendered. Must not be <see langword="null"/> and must have positive
    /// dimensions.</param>
    /// <param name="state">The <see cref="PdfGraphicsState"/> that defines the rendering state for the image.</param>
    public void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state)
    {
        if (processor == null)
        {
            return;
        }

        if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
        {
            return;
        }

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, processor, state);
        softMaskScope.BeginDrawContent();

        var decodingContext = new ImageDecodingContext(state);
        processor.Process(new DrawImageCommand(pdfImage, decodingContext, _factory));
    }
}

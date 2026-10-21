using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Rendering.Form;
using PdfPixel.Rendering.Image;
using PdfPixel.Rendering.Path;
using PdfPixel.Rendering.Shading;
using PdfPixel.Rendering.Text;
using PdfPixel.Shading;
using PdfPixel.Text;
using System;

namespace PdfPixel.Rendering;

/// <summary>
/// Central coordinator for PDF rendering operations with clean delegation.
/// Acts as a facade for the various specialized rendering components.
/// Updated to use PdfFontBase hierarchy.
/// </summary>
public class PdfRenderer : IPdfRenderer
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private readonly PdfTextRenderer _textRenderer;
    private readonly PdfPathRenderer _pathRenderer;
    private readonly PdfImageRenderer _imageRenderer;
    private readonly PdfFormRenderer _formRenderer;
    private readonly PdfShadingRenderer _shadingRenderer;

    internal PdfRenderer(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<PdfRenderer>();

        _textRenderer = new PdfTextRenderer(this, _loggerFactory);
        _pathRenderer = new PdfPathRenderer(this, _loggerFactory);
        _imageRenderer = new PdfImageRenderer(this, _loggerFactory);
        _formRenderer = new PdfFormRenderer(this, _loggerFactory);
        _shadingRenderer = new PdfShadingRenderer(this, _loggerFactory);
    }


    /// <summary>
    /// Draw text with positioning adjustments (if any) and return total advancement.
    /// </summary>
    public PdfSize DrawTextSequence(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFont font)
        => _textRenderer.DrawTextSequence(processor, glyphs, state, font);

    /// <summary>
    /// Draw a path with the specified operation and fill type.
    /// </summary>
    public void DrawPath(IPdfCommandProcessor processor, PdfPath path, PdfGraphicsState state, PdfPaintOperation operation) => _pathRenderer.DrawPath(processor, path, state, operation);

    /// <summary>
    /// Draw an image in PDF unit coordinate space.
    /// </summary>
    public void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state) => _imageRenderer.DrawImage(processor, pdfImage, state);

    /// <summary>
    /// Render a form XObject with proper handling of transparency and soft masks.
    /// </summary>
    public void DrawForm(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState) => _formRenderer.DrawForm(processor, formXObject, graphicsState);

    /// <summary>
    /// Draw a shading fill described by the shading dictionary (operator 'sh').
    /// Soft mask application is delegated to the shading drawer implementation.
    /// Caller must apply the appropriate CTM prior to invocation.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    /// <param name="shading">Shading object defining the gradient/pattern.</param>
    /// <param name="state">Current graphics state providing CTM and soft mask.</param>
    public void DrawShading(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state) => _shadingRenderer.DrawShading(processor, shading, state);
}

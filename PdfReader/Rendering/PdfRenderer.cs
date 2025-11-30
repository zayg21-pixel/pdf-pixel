using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
using PdfReader.Fonts.Model;
using PdfReader.Forms;
using PdfReader.Imaging.Model;
using PdfReader.Rendering.Form;
using PdfReader.Rendering.Image;
using PdfReader.Rendering.Path;
using PdfReader.Rendering.Shading;
using PdfReader.Rendering.State;
using PdfReader.Rendering.Text;
using PdfReader.Shading.Model;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering;

/// <summary>
/// Central coordinator for PDF rendering operations with clean delegation
/// Acts as a facade for the various specialized rendering components
/// Updated to use PdfFontBase hierarchy
/// </summary>
public class PdfRenderer : IPdfRenderer
{
    private readonly IPdfTextRenderer _textRenderer;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private readonly IPathRenderer _pathRenderer;
    private readonly IImageRenderer _imageRenderer;
    private readonly IFormRenderer _formRenderer;
    private readonly IShadingRenderer _shadingRenderer;

    internal PdfRenderer(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<PdfRenderer>();
        _textRenderer = new PdfTextRenderer(this, _loggerFactory);
        _pathRenderer = new PathRenderer(this, _loggerFactory);
        _imageRenderer = new ImageRenderer(this, _loggerFactory);
        _formRenderer = new FormRenderer(this, _loggerFactory);
        _shadingRenderer = new ShadingRenderer(this, _loggerFactory);
    }


    /// <summary>
    /// Draw text with positioning adjustments (if any) and return total advancement.
    /// </summary>
    public float DrawTextSequence(SKCanvas canvas, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        return _textRenderer.DrawTextSequence(canvas, glyphs, state, font);
    }

    /// <summary>
    /// Draw a path with the specified operation and fill type.
    /// </summary>
    public void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, SKPathFillType fillType)
    {
        _pathRenderer.DrawPath(canvas, path, state, operation, fillType);
    }

    /// <summary>
    /// Draw an image in PDF unit coordinate space..
    /// </summary>
    public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state)
    {
        _imageRenderer.DrawImage(canvas, pdfImage, state);
    }

    /// <summary>
    /// Render a form XObject onto the canvas with proper handling of transparency and soft masks.
    /// </summary>
    public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState, HashSet<uint> processingXObjects)
    {
        _formRenderer.DrawForm(canvas, formXObject, graphicsState, processingXObjects);
    }

    /// <summary>
    /// Draw a shading fill described by the shading dictionary (operator 'sh').
    /// Soft mask application is delegated to the shading drawer implementation.
    /// Caller must apply the appropriate CTM prior to invocation.
    /// </summary>
    /// <param name="canvas">Destination canvas to draw on.</param>
    /// <param name="shading">Shading object defining the gradient/pattern.</param>
    /// <param name="state">Current graphics state providing CTM and soft mask.</param>
    public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state)
    {
        _shadingRenderer.DrawShading(canvas, shading, state);
    }
}
using PdfPixel.Color;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;

namespace PdfPixel.Rendering;

/// <summary>
/// Represents single drawing item for composition of paint and
/// content. Also performs required content transformations
/// to active coordinates.
/// </summary>
internal interface IRenderTarget
{
    /// <summary>
    /// Request to render single drawing item.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    void Render(IPdfCommandProcessor processor);

    /// <summary>
    /// Area the content of this render target can cover, in current CTM space.
    /// Always a rectangle that contains everything <see cref="Render"/> draws.
    /// </summary>
    PdfRectangle Bounds { get; }

    /// <summary>
    /// Returns color of the render target if applicable.
    /// </summary>
    PdfColor Color { get; }

    /// <summary>
    /// Called by pattern implementations before they emit their tile/shading commands.
    /// Path/text targets apply their clip path; image targets open a new layer.
    /// <paramref name="patternBounds"/> is the area the pattern paints in current CTM space,
    /// or null when the pattern covers the whole target.
    /// </summary>
    void BeforePatternRender(IPdfCommandProcessor processor, PdfRectangle? patternBounds);

    /// <summary>
    /// Called by pattern implementations after all tile/shading commands have been emitted.
    /// No-op for path/text targets; image targets apply the stencil mask and close the layer.
    /// </summary>
    void AfterPatternRender(IPdfCommandProcessor processor);
}

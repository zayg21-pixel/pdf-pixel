using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;

namespace PdfPixel.Rendering;

/// <summary>
/// Represents single drawing item for composition of paint and
/// content. Also performs required content transformations
/// to active coordinates.
/// </summary>
// TODO: remove; no code uses the interface as a type, only its implementations reference it.
internal interface IRenderTarget
{
    /// <summary>
    /// Request to render single drawing item.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    void Render(IPdfCommandProcessor processor);

    /// <summary>
    /// Returns paint of the render target.
    /// </summary>
    PdfPaint Paint { get; }
}

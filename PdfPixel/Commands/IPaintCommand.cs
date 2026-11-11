using PdfPixel.Color.Paint;
using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Exposes the paint a command applies when drawing and the area it paints.
/// </summary>
public interface IPaintCommand
{
    /// <summary>
    /// Gets the paint this command applies, or <see langword="null"/> when the command draws without a paint override.
    /// </summary>
    PdfPaint? Paint { get; }

    /// <summary>
    /// Gets the area the command paints, in the space it is drawn in, or <see langword="null"/> when it is not bounded.
    /// </summary>
    PdfRectangle? Bounds { get; }
}

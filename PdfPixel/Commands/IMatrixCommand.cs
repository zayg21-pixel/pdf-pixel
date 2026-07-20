using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Exposes the matrix a command concatenates onto the canvas before performing its own work.
/// </summary>
public interface IMatrixCommand
{
    /// <summary>
    /// Gets the matrix this command concatenates onto the canvas.
    /// </summary>
    PdfMatrix Matrix { get; }
}

using PdfPixel.Geometry;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Exposes the matrix a command concatenates onto the current transformation before performing its own work.
/// </summary>
public interface IMatrixCommand
{
    /// <summary>
    /// Gets the matrix this command concatenates onto the current transformation.
    /// </summary>
    PdfMatrix Matrix { get; }
}

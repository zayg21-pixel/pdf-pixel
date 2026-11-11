using PdfPixel.Geometry.PathOperations;

namespace PdfPixel.Geometry;

/// <summary>
/// Receives the straight lines <see cref="PdfPathFlattener"/> turns a path into.
/// </summary>
public interface IPdfLineSink
{
    /// <summary>
    /// Receives one line, from <paramref name="lineStart"/> to <paramref name="lineEnd"/>.
    /// </summary>
    void AddLine(in PdfPoint lineStart, in PdfPoint lineEnd);
}

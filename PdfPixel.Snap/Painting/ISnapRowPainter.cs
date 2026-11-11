using System;

namespace PdfPixel.Snap.Painting;

/// <summary>
/// Paints the pixels of one row of a draw, given the shape of each pixel.
/// </summary>
internal interface ISnapRowPainter
{
    /// <summary>
    /// Paints <paramref name="shapes"/>.Length pixels of row <paramref name="y"/> from column <paramref name="left"/>;
    /// the painter may change <paramref name="shapes"/>.
    /// </summary>
    void PaintRow(int y, int left, Span<float> shapes);
}

using PdfPixel.Color.Paint;
using System;

namespace PdfPixel.Snap.Painting;

/// <summary>
/// Paints every pixel of a row in one color and alpha.
/// </summary>
internal readonly struct SnapSolidRowPainter : ISnapRowPainter
{
    private readonly PdfSnapSurface _surface;
    private readonly float[] _color;
    private readonly float _alpha;
    private readonly PdfPaint _paint;

    public SnapSolidRowPainter(PdfSnapSurface surface, float[] color, float alpha, PdfPaint paint)
    {
        _surface = surface;
        _color = color;
        _alpha = alpha;
        _paint = paint;
    }

    /// <inheritdoc />
    public void PaintRow(int y, int left, Span<float> shapes)
        => _surface.PaintRow(y, left, shapes, new SnapRowValues(_color, true), SnapRowValues.One, _alpha, _paint);
}

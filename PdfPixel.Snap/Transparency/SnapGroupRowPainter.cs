using PdfPixel.Color.Paint;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using System;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// Paints a row of a closed group as an element's source, through the shape of the element's geometry.
/// </summary>
internal readonly struct SnapGroupRowPainter : ISnapRowPainter
{
    private readonly PdfSnapSurface _surface;
    private readonly SnapLayer _group;
    private readonly SnapColorConverter? _converter;
    private readonly PdfPaint _paint;

    public SnapGroupRowPainter(PdfSnapSurface surface, SnapLayer group, PdfPaint paint)
    {
        _surface = surface;
        _group = group;
        _converter = surface.Target.GetGroupConverter(group, paint);
        _paint = paint;
    }

    /// <inheritdoc />
    public void PaintRow(int y, int left, Span<float> shapes)
    {
        SnapRowValues coverage = new(shapes, false);
        SnapRowValues mask = _surface.GetMaskRow(y, left, shapes.Length);

        _surface.Target.ComposeGroupRow(y, left, shapes.Length, _group, _converter, coverage, mask, _paint);
    }
}

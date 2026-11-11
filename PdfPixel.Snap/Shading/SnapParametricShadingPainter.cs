using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Snap.Painting;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// Paints a row of an axial or radial shading from the ramp color at each pixel center's parameter.
/// </summary>
internal readonly struct SnapParametricShadingPainter : ISnapRowPainter
{
    private readonly PdfSnapSurface _surface;
    private readonly SnapGradientRamp _ramp;
    private readonly SnapShadingParameter _getParameter;
    private readonly PdfMatrix _deviceToShading;
    private readonly PdfPaint _paint;

    public SnapParametricShadingPainter(PdfSnapSurface surface, SnapGradientRamp ramp, SnapShadingParameter getParameter, in PdfMatrix deviceToShading, PdfPaint paint)
    {
        _surface = surface;
        _ramp = ramp;
        _getParameter = getParameter;
        _deviceToShading = deviceToShading;
        _paint = paint;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void PaintRow(int y, int left, Span<float> shapes)
    {
        int count = shapes.Length;
        int componentCount = _surface.ComponentCount;
        Span<float> colors = _surface.GetRowColors(count);
        PdfPoint rowStart = _deviceToShading.MapPoint(new PdfPoint(left + 0.5f, y + 0.5f));
        ref float shape = ref MemoryMarshal.GetReference(shapes);
        int colorOffset = 0;

        for (int index = 0; index < count; index++)
        {
            if (shape > 0)
            {
                float? parameter = _getParameter(new PdfPoint(rowStart.X + (index * _deviceToShading.ScaleX), rowStart.Y + (index * _deviceToShading.SkewY)));

                if (parameter == null)
                {
                    shape = 0;
                }
                else
                {
                    _ramp.GetColor(parameter.Value).CopyTo(colors.Slice(colorOffset, componentCount));
                }
            }

            shape = ref Unsafe.Add(ref shape, 1);
            colorOffset += componentCount;
        }

        _surface.PaintRow(y, left, shapes, new SnapRowValues(colors, false), SnapRowValues.One, _paint.Alpha, _paint);
    }
}

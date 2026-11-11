using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Transparency;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Pattern;

/// <summary>
/// Paints a row of a tiling pattern from its rasterized window, each pixel taking the window pixel its center maps to,
/// composited like a group.
/// </summary>
internal readonly struct SnapTilingPeriodPainter : ISnapRowPainter
{
    private readonly PdfSnapSurface _surface;
    private readonly SnapTilingWindow _window;
    private readonly PdfMatrix _deviceToPattern;
    private readonly PdfPaint _paint;

    public SnapTilingPeriodPainter(PdfSnapSurface surface, SnapTilingWindow window, in PdfMatrix deviceToPattern, PdfPaint paint)
    {
        _surface = surface;
        _window = window;
        _deviceToPattern = deviceToPattern;
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
        Span<float> objectShapes = _surface.GetRowObjectShapes(count);
        Span<float> alphas = _surface.GetRowAlphas(count);
        PdfPoint rowStart = _deviceToPattern.MapPoint(new PdfPoint(left + 0.5f, y + 0.5f));
        var colorByteCount = (uint)(componentCount * sizeof(float));
        ref float shape = ref MemoryMarshal.GetReference(shapes);
        ref float color = ref MemoryMarshal.GetReference(colors);
        ref float objectShape = ref MemoryMarshal.GetReference(objectShapes);
        ref float alpha = ref MemoryMarshal.GetReference(alphas);
        ref float windowColors = ref MemoryMarshal.GetReference(_window.Colors);
        ref float windowShapes = ref MemoryMarshal.GetReference(_window.Shapes);
        ref float windowAlphas = ref MemoryMarshal.GetReference(_window.Alphas);

        for (int index = 0; index < count; index++)
        {
            if (shape > 0)
            {
                float patternX = rowStart.X + (index * _deviceToPattern.ScaleX);
                float patternY = rowStart.Y + (index * _deviceToPattern.SkewY);
                int windowPixel = _window.GetPixel(patternX, patternY);

                Unsafe.CopyBlockUnaligned(ref Unsafe.As<float, byte>(ref color), ref Unsafe.As<float, byte>(ref Unsafe.Add(ref windowColors, windowPixel * componentCount)), colorByteCount);
                objectShape = Unsafe.Add(ref windowShapes, windowPixel);
                alpha = Unsafe.Add(ref windowAlphas, windowPixel);
            }
            else
            {
                objectShape = 0;
                alpha = 0;
            }

            shape = ref Unsafe.Add(ref shape, 1);
            color = ref Unsafe.Add(ref color, componentCount);
            objectShape = ref Unsafe.Add(ref objectShape, 1);
            alpha = ref Unsafe.Add(ref alpha, 1);
        }

        SnapRowValues colorValues = new(colors, false);
        SnapRowValues coverage = new(shapes, false);
        SnapRowValues objectShapeValues = new(objectShapes, false);
        SnapRowValues alphaValues = new(alphas, false);
        SnapRowValues mask = _surface.GetMaskRow(y, left, count);
        SnapSourceRow row = new(y, left, count, colorValues, coverage, objectShapeValues, alphaValues, mask, _paint.Alpha, _paint.AlphaIsShape);

        _surface.Target.Compose(row, _paint.BlendMode);
    }
}

using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Snap.Painting;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// Paints a row of a function-based shading, interpolated from the grid of colors it was sampled into.
/// </summary>
internal readonly struct SnapFunctionShadingPainter : ISnapRowPainter
{
    private readonly PdfSnapSurface _surface;
    private readonly float[] _gridColors;
    private readonly int _width;
    private readonly int _height;
    private readonly PdfMatrix _deviceToGrid;
    private readonly PdfPaint _paint;

    public SnapFunctionShadingPainter(PdfSnapSurface surface, float[] gridColors, int width, int height, in PdfMatrix deviceToGrid, PdfPaint paint)
    {
        _surface = surface;
        _gridColors = gridColors;
        _width = width;
        _height = height;
        _deviceToGrid = deviceToGrid;
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
        PdfPoint rowStart = _deviceToGrid.MapPoint(new PdfPoint(left + 0.5f, y + 0.5f));
        ref float shape = ref MemoryMarshal.GetReference(shapes);
        ref float color = ref MemoryMarshal.GetReference(colors);

        for (int index = 0; index < count; index++)
        {
            if (shape > 0)
            {
                PdfPoint gridPoint = new(rowStart.X + (index * _deviceToGrid.ScaleX), rowStart.Y + (index * _deviceToGrid.SkewY));
                SampleBilinear(gridPoint, componentCount, ref color);
            }

            shape = ref Unsafe.Add(ref shape, 1);
            color = ref Unsafe.Add(ref color, componentCount);
        }

        _surface.PaintRow(y, left, shapes, new SnapRowValues(colors, false), SnapRowValues.One, _paint.Alpha, _paint);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void SampleBilinear(in PdfPoint point, int componentCount, ref float color)
    {
        float gridX = point.X - 0.5f;
        float gridY = point.Y - 0.5f;
        var left = (int)Math.Floor(gridX);
        var top = (int)Math.Floor(gridY);
        float horizontalWeight = gridX - left;
        float verticalWeight = gridY - top;
        ref float gridColors = ref MemoryMarshal.GetReference(_gridColors.AsSpan());
        ref float topLeft = ref Unsafe.Add(ref gridColors, GetCell(left, top) * componentCount);
        ref float topRight = ref Unsafe.Add(ref gridColors, GetCell(left + 1, top) * componentCount);
        ref float bottomLeft = ref Unsafe.Add(ref gridColors, GetCell(left, top + 1) * componentCount);
        ref float bottomRight = ref Unsafe.Add(ref gridColors, GetCell(left + 1, top + 1) * componentCount);

        for (int component = 0; component < componentCount; component++)
        {
            float topLeftValue = Unsafe.Add(ref topLeft, component);
            float bottomLeftValue = Unsafe.Add(ref bottomLeft, component);
            float upper = topLeftValue + ((Unsafe.Add(ref topRight, component) - topLeftValue) * horizontalWeight);
            float lower = bottomLeftValue + ((Unsafe.Add(ref bottomRight, component) - bottomLeftValue) * horizontalWeight);
            Unsafe.Add(ref color, component) = upper + ((lower - upper) * verticalWeight);
        }
    }

    private int GetCell(int column, int row)
    {
        int clampedColumn = Math.Max(0, Math.Min(_width - 1, column));
        int clampedRow = Math.Max(0, Math.Min(_height - 1, row));

        return (clampedRow * _width) + clampedColumn;
    }
}

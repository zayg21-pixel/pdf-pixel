using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Snap.Painting;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// Paints the colors gathered from a mesh's triangles once per pixel, skipping pixels no triangle claimed.
/// </summary>
internal readonly struct SnapMeshPainter : ISnapRowPainter
{
    private readonly PdfSnapSurface _surface;
    private readonly float[] _colors;
    private readonly bool[] _hasColor;
    private readonly PdfIntegerRectangle _meshBounds;
    private readonly PdfPaint _paint;

    public SnapMeshPainter(PdfSnapSurface surface, float[] colors, bool[] hasColor, in PdfIntegerRectangle meshBounds, PdfPaint paint)
    {
        _surface = surface;
        _colors = colors;
        _hasColor = hasColor;
        _meshBounds = meshBounds;
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
        int rowStart = ((y - _meshBounds.Top) * _meshBounds.Width) + (left - _meshBounds.Left);
        ref bool hasColor = ref _hasColor[rowStart];
        ref float shape = ref MemoryMarshal.GetReference(shapes);

        for (int index = 0; index < count; index++)
        {
            if (!hasColor)
            {
                shape = 0;
            }

            hasColor = ref Unsafe.Add(ref hasColor, 1);
            shape = ref Unsafe.Add(ref shape, 1);
        }

        SnapRowValues colors = new(_colors.AsSpan(rowStart * componentCount, count * componentCount), false);

        _surface.PaintRow(y, left, shapes, colors, SnapRowValues.One, _paint.Alpha, _paint);
    }
}

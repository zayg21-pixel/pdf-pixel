using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// Gathers the Gouraud-interpolated colors of one mesh triangle at the pixel centers it contains, for the whole mesh to
/// be painted once afterwards.
/// </summary>
internal readonly struct SnapMeshTrianglePainter : ISnapRowPainter
{
    private readonly SnapColorConverter _converter;
    private readonly PdfPoint _point0;
    private readonly PdfPoint _point1;
    private readonly PdfPoint _point2;
    private readonly PdfColor _color0;
    private readonly PdfColor _color1;
    private readonly PdfColor _color2;
    private readonly float _inverseArea;
    private readonly float[] _components;
    private readonly PdfIntegerRectangle _meshBounds;
    private readonly int _componentCount;
    private readonly float[] _colors;
    private readonly bool[] _hasColor;

    /// <summary>
    /// Initializes a painter for a triangle in pixel coordinates whose doubled signed area is
    /// <paramref name="doubleArea"/>.
    /// </summary>
    public SnapMeshTrianglePainter(
        SnapColorConverter converter,
        in PdfPoint point0,
        in PdfPoint point1,
        in PdfPoint point2,
        in PdfColor color0,
        in PdfColor color1,
        in PdfColor color2,
        float doubleArea,
        float[] components,
        in PdfIntegerRectangle meshBounds,
        int componentCount,
        float[] colors,
        bool[] hasColor)
    {
        _converter = converter;
        _point0 = point0;
        _point1 = point1;
        _point2 = point2;
        _color0 = color0;
        _color1 = color1;
        _color2 = color2;
        _inverseArea = 1 / doubleArea;
        _components = components;
        _meshBounds = meshBounds;
        _componentCount = componentCount;
        _colors = colors;
        _hasColor = hasColor;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void PaintRow(int y, int left, Span<float> shapes)
    {
        ref float color0 = ref MemoryMarshal.GetReference(_color0.Components.Span);
        ref float color1 = ref MemoryMarshal.GetReference(_color1.Components.Span);
        ref float color2 = ref MemoryMarshal.GetReference(_color2.Components.Span);
        ref float components = ref MemoryMarshal.GetReference(_components.AsSpan());
        int componentCount = _components.Length;
        float centerY = y + 0.5f;
        int rowStart = ((y - _meshBounds.Top) * _meshBounds.Width) + (left - _meshBounds.Left);
        ref float shape = ref MemoryMarshal.GetReference(shapes);
        ref bool hasColor = ref Unsafe.Add(ref MemoryMarshal.GetReference(_hasColor.AsSpan()), rowStart);
        ref float color = ref Unsafe.Add(ref MemoryMarshal.GetReference(_colors.AsSpan()), rowStart * _componentCount);

        for (int index = 0; index < shapes.Length; index++)
        {
            if (shape > 0)
            {
                float centerX = left + index + 0.5f;

                // The center is inside; clamping only absorbs rounding right on an edge.
                float weight1 = Math.Max(0, (((centerX - _point0.X) * (_point2.Y - _point0.Y)) - ((_point2.X - _point0.X) * (centerY - _point0.Y))) * _inverseArea);
                float weight2 = Math.Max(0, (((_point1.X - _point0.X) * (centerY - _point0.Y)) - ((centerX - _point0.X) * (_point1.Y - _point0.Y))) * _inverseArea);
                float weight0 = Math.Max(0, 1 - weight1 - weight2);
                float weightSum = weight0 + weight1 + weight2;

                for (int component = 0; component < componentCount; component++)
                {
                    float weighted = (weight0 * Unsafe.Add(ref color0, component))
                        + (weight1 * Unsafe.Add(ref color1, component))
                        + (weight2 * Unsafe.Add(ref color2, component));
                    Unsafe.Add(ref components, component) = weighted / weightSum;
                }

                _converter.Convert(ref components, ref color);
                hasColor = true;
            }

            shape = ref Unsafe.Add(ref shape, 1);
            hasColor = ref Unsafe.Add(ref hasColor, 1);
            color = ref Unsafe.Add(ref color, _componentCount);
        }
    }
}

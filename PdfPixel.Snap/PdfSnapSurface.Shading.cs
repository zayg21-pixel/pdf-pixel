using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Shading;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using PdfPixel.Snap.Shading;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <summary>
    /// Paints <paramref name="gradient"/> wherever the current clip allows.
    /// </summary>
    /// <param name="gradient">The gradient, in current user space.</param>
    /// <param name="paint">The paint the gradient's colors are converted and composited with.</param>
    public void DrawAxialGradient(PdfAxialGradient gradient, PdfPaint paint)
    {
        if (gradient == null)
        {
            throw new ArgumentNullException(nameof(gradient));
        }

        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        PrepareTextObjectDraw(paint);
        PaintAxialGradient(gradient, paint, bounds: null);
    }

    /// <summary>
    /// Paints <paramref name="gradient"/> wherever the current clip allows.
    /// </summary>
    /// <param name="gradient">The gradient, in current user space.</param>
    /// <param name="paint">The paint the gradient's colors are converted and composited with.</param>
    public void DrawRadialGradient(PdfRadialGradient gradient, PdfPaint paint)
    {
        if (gradient == null)
        {
            throw new ArgumentNullException(nameof(gradient));
        }

        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        PrepareTextObjectDraw(paint);
        PaintRadialGradient(gradient, paint, bounds: null);
    }

    /// <summary>
    /// Paints the Gouraud-shaded triangles of <paramref name="vertices"/>.
    /// </summary>
    /// <param name="vertices">The triangles, in current user space.</param>
    /// <param name="paint">The paint the vertex colors are converted and composited with.</param>
    public void DrawVertices(PdfVertices vertices, PdfPaint paint)
    {
        if (vertices == null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        PrepareTextObjectDraw(paint);
        PaintVertices(vertices, paint);
    }

    /// <summary>
    /// Draws a shading evaluated per pixel from its definition (ISO 32000-2 8.7.4.5); meshes and function grids paint by
    /// pixel centers, and the BBox clips like any clip (Table 77).
    /// </summary>
    private void DrawShading(PdfShadingContent content, PdfPaint paint)
    {
        if (IsEmpty)
        {
            return;
        }

        PrepareTextObjectDraw(paint);

        if (content.Axial != null)
        {
            PaintAxialGradient(content.Axial, paint, content.GetBounds());
        }
        else if (content.Radial != null)
        {
            PaintRadialGradient(content.Radial, paint, content.GetBounds());
        }
        else if (content.Mesh != null)
        {
            PaintVertices(content.Mesh, paint);
        }
        else if (content.Function != null)
        {
            PaintFunctionShading(content.Function, _state.Target.GetConverter(paint), paint);
        }
    }

    /// <summary>
    /// Paints an axial gradient within <paramref name="bounds"/>, or wherever the clip allows when it has none.
    /// </summary>
    private void PaintAxialGradient(PdfAxialGradient axial, PdfPaint paint, PdfRectangle? bounds)
    {
        float deviceLength = SnapShadingGeometry.GetAxialDeviceLength(axial, _state.DeviceMatrix);
        SnapGradientRamp ramp = new(axial.Colors, axial.Positions, deviceLength, _state.Target.GetConverter(paint), ComponentCount);

        PaintParametricShading(ramp, paint, bounds, (in PdfPoint point) => SnapShadingGeometry.GetAxialParameter(axial, point));
    }

    /// <summary>
    /// Paints a radial gradient within <paramref name="bounds"/>, or wherever the clip allows when it has none.
    /// </summary>
    private void PaintRadialGradient(PdfRadialGradient radial, PdfPaint paint, PdfRectangle? bounds)
    {
        float deviceLength = SnapShadingGeometry.GetRadialDeviceLength(radial, _state.DeviceMatrix);
        SnapGradientRamp ramp = new(radial.Colors, radial.Positions, deviceLength, _state.Target.GetConverter(paint), ComponentCount);

        PaintParametricShading(ramp, paint, bounds, (in PdfPoint point) => SnapShadingGeometry.GetRadialParameter(radial, point));
    }

    private void PaintParametricShading(SnapGradientRamp ramp, PdfPaint paint, PdfRectangle? bounds, SnapShadingParameter getParameter)
    {
        SnapParametricShadingPainter painter = new(this, ramp, getParameter, _state.DeviceMatrix.Invert(), paint);

        if (bounds != null)
        {
            PdfPathBuilder builder = new();
            builder.AddRect(bounds.Value);

            DrawShape(builder.ToPath(), snapsRectangle: !IsType3GlyphContent(), painter);
        }
        else
        {
            Draw(GetDrawLimits(), PdfPathFillType.Winding, _executionContext.Parameters.Antialias, painter);
        }
    }

    /// <summary>
    /// Paints a mesh of Gouraud-shaded triangles: each claims the pixel centers it contains, and the gathered colors are
    /// painted once per pixel, so shared edges show no seam.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void PaintVertices(PdfVertices mesh, PdfPaint paint)
    {
        int triangleCount = (mesh.Indices != null) ? mesh.Indices.Length / 3 : mesh.Positions.Length / 3;

        if (triangleCount == 0)
        {
            return;
        }

        SnapColorConverter converter = _state.Target.GetConverter(paint);
        PdfMatrix deviceMatrix = _state.DeviceMatrix;
        var devicePositions = new PdfPoint[mesh.Positions.Length];
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int index = 0; index < devicePositions.Length; index++)
        {
            PdfPoint position = deviceMatrix.MapPoint(mesh.Positions[index]);
            devicePositions[index] = position;
            minX = Math.Min(minX, position.X);
            minY = Math.Min(minY, position.Y);
            maxX = Math.Max(maxX, position.X);
            maxY = Math.Max(maxY, position.Y);
        }

        PdfIntegerRectangle meshBounds = PdfIntegerRectangle.Intersect(PdfSnapGrid.RoundOut(new PdfRectangle(minX, minY, maxX, maxY)), GetDrawLimits());

        if (meshBounds.Width <= 0 || meshBounds.Height <= 0)
        {
            return;
        }

        int componentCount = ComponentCount;
        var colors = new float[meshBounds.Width * meshBounds.Height * componentCount];
        var hasColor = new bool[meshBounds.Width * meshBounds.Height];
        var components = new float[mesh.Colors[0].Components.Length];

        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            GetTriangle(mesh, triangle, out int first, out int second, out int third);
            ShadeTriangle(devicePositions, mesh.Colors, first, second, third, meshBounds, converter, components, colors, hasColor);
        }

        SnapMeshPainter painter = new(this, colors, hasColor, meshBounds, paint);
        Draw(meshBounds, PdfPathFillType.Winding, areaCoverage: false, painter);
    }

    private static void GetTriangle(PdfVertices mesh, int triangle, out int first, out int second, out int third)
    {
        int start = triangle * 3;

        if (mesh.Indices != null)
        {
            first = mesh.Indices[start];
            second = mesh.Indices[start + 1];
            third = mesh.Indices[start + 2];
        }
        else
        {
            first = start;
            second = start + 1;
            third = start + 2;
        }
    }

    private void ShadeTriangle(
        PdfPoint[] positions,
        PdfColor[] vertexColors,
        int first,
        int second,
        int third,
        in PdfIntegerRectangle meshBounds,
        SnapColorConverter converter,
        float[] components,
        float[] colors,
        bool[] hasColor)
    {
        PdfPoint point0 = positions[first];
        PdfPoint point1 = positions[second];
        PdfPoint point2 = positions[third];
        float doubleArea = ((point1.X - point0.X) * (point2.Y - point0.Y)) - ((point2.X - point0.X) * (point1.Y - point0.Y));

        if (Math.Abs(doubleArea) <= 1e-12f)
        {
            return;
        }

        SnapEdgeList edges = Scratch.Edges;
        edges.Clear();
        edges.AddLine(point0, point1);
        edges.AddLine(point1, point2);
        edges.AddLine(point2, point0);

        SnapMeshTrianglePainter painter = new(
            converter,
            point0,
            point1,
            point2,
            vertexColors[first],
            vertexColors[second],
            vertexColors[third],
            doubleArea,
            components,
            meshBounds,
            ComponentCount,
            colors,
            hasColor);
        SnapShapeRowSink<SnapMeshTrianglePainter> sink = new(painter, Scratch);

        Scratch.Rasterizer.Rasterize(edges, PdfPathFillType.Winding, areaCoverage: false, meshBounds, ref sink);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void PaintFunctionShading(PdfShadingImage image, SnapColorConverter converter, PdfPaint paint)
    {
        PdfImageSamples samples = image.Samples;

        if (samples.Format != PdfImageSampleFormat.Float)
        {
            ReportUnsupported(PdfCommandKind.DrawShading);
            return;
        }

        int width = samples.Width;
        int height = samples.Height;
        int sourceComponentCount = samples.Channels.Count;
        int componentCount = ComponentCount;
        var gridColors = new float[width * height * componentCount];

        ref float gridColor = ref MemoryMarshal.GetReference(gridColors.AsSpan());

        for (int row = 0; row < height; row++)
        {
            ref float rowValue = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(samples.GetRow(row)));

            for (int column = 0; column < width; column++)
            {
                converter.Convert(ref rowValue, ref gridColor);
                rowValue = ref Unsafe.Add(ref rowValue, sourceComponentCount);
                gridColor = ref Unsafe.Add(ref gridColor, componentCount);
            }
        }

        // The sampled grid is drawn like an image: the pixels whose centres it covers.
        PdfMatrix placement = _state.DeviceMatrix.PreConcat(image.Matrix);
        PdfRectangle gridFootprint = new(0, 0, width, height);
        PdfIntegerRectangle? pixels = PdfSnapGrid.GetImagePixels(gridFootprint, placement);

        if (pixels == null)
        {
            PdfSnapGrid.AddImageFootprint(Scratch.Edges, gridFootprint, placement);
        }

        SnapFunctionShadingPainter painter = new(this, gridColors, width, height, placement.Invert(), paint);
        Draw(pixels, PdfPathFillType.Winding, areaCoverage: false, painter);
    }
}

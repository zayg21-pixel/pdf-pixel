using PdfPixel.Geometry;
using PdfPixel.Geometry.PathOperations;
using PdfPixel.Objects;
using System;

namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// The non-horizontal edges of one outline to be filled, in pixel coordinates, with the bounds they span.
/// </summary>
internal sealed class SnapEdgeList : IPdfLineSink
{
    // Largest distance, in device pixels, a flattened curve may stray from the true curve (ISO 32000-2 10.7.2).
    private const float FlatteningTolerance = 0.1f;

    private readonly PdfValueList<SnapEdge> _edges = new();

    public ReadOnlySpan<SnapEdge> Edges => _edges.Items;

    public float MinX { get; private set; } = float.MaxValue;

    public float MinY { get; private set; } = float.MaxValue;

    public float MaxX { get; private set; } = float.MinValue;

    public float MaxY { get; private set; } = float.MinValue;

    public void Clear()
    {
        _edges.Clear();
        MinX = float.MaxValue;
        MinY = float.MaxValue;
        MaxX = float.MinValue;
        MaxY = float.MinValue;
    }

    /// <summary>
    /// Adds the edges of <paramref name="path"/> mapped into pixels, every subpath closed as a fill closes it.
    /// </summary>
    public void AddPath(PdfPath path, in PdfMatrix deviceMatrix)
    {
        var sink = this;
        PdfPathFlattener.Flatten(path, deviceMatrix, FlatteningTolerance, ref sink);
    }

    /// <inheritdoc />
    public void AddLine(in PdfPoint lineStart, in PdfPoint lineEnd)
    {
        if (lineStart.Y == lineEnd.Y)
        {
            return;
        }

        if (!IsFinite(lineStart.X) || !IsFinite(lineStart.Y) || !IsFinite(lineEnd.X) || !IsFinite(lineEnd.Y))
        {
            return;
        }

        SnapEdge edge = new(lineStart.X, lineStart.Y, lineEnd.X, lineEnd.Y);
        _edges.Add(edge);

        MinX = Math.Min(MinX, Math.Min(lineStart.X, lineEnd.X));
        MaxX = Math.Max(MaxX, Math.Max(lineStart.X, lineEnd.X));
        MinY = Math.Min(MinY, Math.Min(lineStart.Y, lineEnd.Y));
        MaxY = Math.Max(MaxY, Math.Max(lineStart.Y, lineEnd.Y));
    }

    /// <summary>
    /// Adds the closed quadrilateral through the four corners in order.
    /// </summary>
    public void AddQuadrilateral(in PdfPoint first, in PdfPoint second, in PdfPoint third, in PdfPoint fourth)
    {
        AddLine(first, second);
        AddLine(second, third);
        AddLine(third, fourth);
        AddLine(fourth, first);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

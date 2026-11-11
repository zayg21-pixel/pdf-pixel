using PdfPixel.Geometry;
using PdfPixel.Snap.Rasterization;
using System;

namespace PdfPixel.Snap.Clipping;

/// <summary>
/// An immutable clipping region (ISO 32000-2 10.7.4) stored as runs of pixels per row, each fully or partly inside.
/// </summary>
internal sealed class SnapClip
{
    private readonly SnapClipRun[] _runs;
    private readonly int[]? _rowStarts;
    private readonly float[] _values;
    private readonly int _top;

    private SnapClip(in PdfIntegerRectangle bounds, SnapClipRun[] runs, int[]? rowStarts, float[] values, int top)
    {
        Bounds = bounds;
        _runs = runs;
        _rowStarts = rowStarts;
        _values = values;
        _top = top;
    }

    /// <summary>
    /// Gets the area outside of which the clip excludes every pixel.
    /// </summary>
    public PdfIntegerRectangle Bounds { get; }

    /// <summary>
    /// Gets whether every pixel inside <see cref="Bounds"/> is fully inside the clip.
    /// </summary>
    public bool IsRectangle => _rowStarts == null;

    /// <summary>
    /// Returns the runs of row <paramref name="y"/>, none for a row outside <see cref="Bounds"/>.
    /// </summary>
    public SnapClipRow GetRow(int y)
    {
        if (y < Bounds.Top || y >= Bounds.Bottom)
        {
            return new SnapClipRow(y, Bounds.Left, Bounds.Right, ReadOnlySpan<SnapClipRun>.Empty, ReadOnlySpan<float>.Empty);
        }
        else if (_rowStarts == null)
        {
            return new SnapClipRow(y, Bounds.Left, Bounds.Right, _runs, _values);
        }
        else
        {
            int start = _rowStarts[y - _top];
            int end = _rowStarts[y - _top + 1];

            return new SnapClipRow(y, Bounds.Left, Bounds.Right, _runs.AsSpan(start, end - start), _values);
        }
    }

    /// <summary>
    /// Returns the region inside both <paramref name="current"/> and the whole-pixel <paramref name="rectangle"/>.
    /// </summary>
    public static SnapClip IntersectRectangle(SnapClip? current, in PdfIntegerRectangle rectangle, in PdfIntegerRectangle surfaceBounds)
    {
        PdfIntegerRectangle currentBounds = (current != null) ? current.Bounds : surfaceBounds;
        PdfIntegerRectangle bounds = Normalize(PdfIntegerRectangle.Intersect(currentBounds, rectangle));

        if (current == null || current.IsRectangle)
        {
            return CreateRectangle(bounds);
        }
        else
        {
            return new SnapClip(bounds, current._runs, current._rowStarts, current._values, current._top);
        }
    }

    /// <summary>
    /// Returns the region inside both <paramref name="current"/> and the outline of <paramref name="edges"/>.
    /// </summary>
    public static SnapClip Intersect(
        SnapClip? current,
        SnapRasterizer rasterizer,
        SnapEdgeList edges,
        PdfPathFillType fillType,
        bool areaCoverage,
        in PdfIntegerRectangle surfaceBounds,
        SnapClipBuffer buffer)
    {
        SnapClip currentClip = current ?? CreateRectangle(surfaceBounds);
        SnapClipBuilder builder = new(currentClip, buffer);
        rasterizer.Rasterize(edges, fillType, areaCoverage, currentClip.Bounds, ref builder);

        return builder.ToClip();
    }

    /// <summary>
    /// Returns a clip that is fully inside over <paramref name="bounds"/>.
    /// </summary>
    public static SnapClip CreateRectangle(in PdfIntegerRectangle bounds)
    {
        SnapClipRun[] runs = [new SnapClipRun(bounds.Left, bounds.Width, isFull: true, valueIndex: 0)];

        return new SnapClip(bounds, runs, null, [], bounds.Top);
    }

    /// <summary>
    /// Returns a clip made of runs per row; <paramref name="rowStarts"/> holds where each row starts in
    /// <paramref name="runs"/> from row <paramref name="top"/>, followed by the end of the last row.
    /// </summary>
    public static SnapClip FromRows(in PdfIntegerRectangle bounds, SnapClipRun[] runs, int[] rowStarts, float[] values, int top)
        => new(bounds, runs, rowStarts, values, top);

    private static PdfIntegerRectangle Normalize(in PdfIntegerRectangle bounds)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            return bounds;
        }
        else
        {
            return PdfIntegerRectangle.Empty;
        }
    }
}

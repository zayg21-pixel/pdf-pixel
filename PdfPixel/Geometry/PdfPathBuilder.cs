using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// Builds a <see cref="PdfPath"/> incrementally into a growable binary buffer. Reusable across many
/// paths via <see cref="Reset"/>, which drops the accumulated segments without releasing the buffer.
/// <see cref="ToPath"/> can be called at any point to snapshot the segments accumulated so far.
/// </summary>
public sealed class PdfPathBuilder
{
    private const int DefaultCapacity = 4;

    private byte[] _buffer = [];
    private int _length;

    /// <summary>
    /// Whether no segments have been accumulated yet.
    /// </summary>
    public bool IsEmpty => _length == 0;

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(float x, float y) => MoveTo(new PdfPoint(x, y));

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(in PdfPoint point)
    {
        Span<PdfPoint> points = stackalloc PdfPoint[1];
        points[0] = point;
        WriteSegment(PdfPathSegmentType.MoveTo, points);
    }

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(float x, float y) => LineTo(new PdfPoint(x, y));

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(in PdfPoint point)
    {
        Span<PdfPoint> points = stackalloc PdfPoint[1];
        points[0] = point;
        WriteSegment(PdfPathSegmentType.LineTo, points);
    }

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(float x1, float y1, float x2, float y2, float x3, float y3)
        => CubicTo(new PdfPoint(x1, y1), new PdfPoint(x2, y2), new PdfPoint(x3, y3));

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(in PdfPoint control1, in PdfPoint control2, in PdfPoint end)
    {
        Span<PdfPoint> points = stackalloc PdfPoint[3];
        points[0] = control1;
        points[1] = control2;
        points[2] = end;
        WriteSegment(PdfPathSegmentType.CubicTo, points);
    }

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    public void Close() => WriteSegment(PdfPathSegmentType.Close, ReadOnlySpan<PdfPoint>.Empty);

    /// <summary>
    /// Appends a copy of another path's segments.
    /// </summary>
    public void AddPath(PdfPath other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        ReadOnlySpan<byte> otherBuffer = other.Buffer.Span;
        EnsureCapacity(_length + otherBuffer.Length);

        otherBuffer.CopyTo(_buffer.AsSpan(_length));
        _length += otherBuffer.Length;
    }

    /// <summary>
    /// Removes all accumulated segments so the builder can be reused. The underlying buffer is kept, not
    /// shrunk, so a builder reused for similarly sized paths does not repeatedly reallocate.
    /// </summary>
    public void Reset() => _length = 0;

    /// <summary>
    /// Snapshots the segments accumulated so far into a new immutable <see cref="PdfPath"/> with the given
    /// fill type. The builder remains usable afterward; the returned path is unaffected by further building
    /// or a later <see cref="Reset"/>.
    /// </summary>
    public PdfPath ToPath(PdfPathFillType fillType = PdfPathFillType.Winding)
    {
        var snapshot = new byte[_length];
        System.Buffer.BlockCopy(_buffer, 0, snapshot, 0, _length);
        return new PdfPath(snapshot, fillType);
    }

    private void WriteSegment(PdfPathSegmentType type, in ReadOnlySpan<PdfPoint> points)
    {
        ReadOnlySpan<byte> pointBytes = MemoryMarshal.AsBytes(points);
        EnsureCapacity(_length + 1 + pointBytes.Length);

        _buffer[_length] = (byte)type;
        _length++;

        pointBytes.CopyTo(_buffer.AsSpan(_length));
        _length += pointBytes.Length;
    }

    private void EnsureCapacity(int requiredLength)
    {
        if (requiredLength <= _buffer.Length)
        {
            return;
        }

        int newCapacity = (_buffer.Length == 0) ? DefaultCapacity : _buffer.Length * 2;
        if (newCapacity < requiredLength)
        {
            newCapacity = requiredLength;
        }

        Array.Resize(ref _buffer, newCapacity);
    }
}

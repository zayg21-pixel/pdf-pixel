using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Geometry;

/// <summary>
/// Builds a <see cref="PdfPath"/> incrementally into a growable binary buffer. Reusable across many
/// paths via <see cref="Reset"/>, which drops the accumulated segments without releasing the buffer.
/// <see cref="ToPath"/> can be called at any point to snapshot the segments accumulated so far.
/// </summary>
public sealed class PdfPathBuilder
{
    private const int DefaultCapacity = 4;

    private readonly PdfMatrix _matrix;
    private readonly bool _isIdentityMatrix;

    private byte[] _buffer = [];
    private int _length;

    /// <summary>
    /// Initializes a builder whose buffer grows from nothing as segments are written, writing points
    /// as they are given.
    /// </summary>
    public PdfPathBuilder()
        : this(0, PdfMatrix.Identity)
    {
    }

    /// <summary>
    /// Initializes a builder whose buffer grows from nothing as segments are written, transforming every
    /// point it is given by <paramref name="matrix"/>, so that the path it builds is already in the space
    /// that matrix maps to.
    /// </summary>
    public PdfPathBuilder(in PdfMatrix matrix)
        : this(0, matrix)
    {
    }

    /// <summary>
    /// Initializes a builder whose buffer starts at <paramref name="capacity"/> bytes, for a caller that
    /// knows roughly how large the path will be and wants to write it without growing on the way, and
    /// whose <paramref name="matrix"/> transforms every point it is given.
    /// </summary>
    public PdfPathBuilder(int capacity, in PdfMatrix matrix)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (capacity > 0)
        {
            _buffer = new byte[capacity];
        }

        _matrix = matrix;
        _isIdentityMatrix = matrix.IsIdentity;
    }

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
        StartSegment(PdfPathSegmentType.MoveTo, pointCount: 1);
        WritePoint(MapPoint(point));
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
        StartSegment(PdfPathSegmentType.LineTo, pointCount: 1);
        WritePoint(MapPoint(point));
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
        StartSegment(PdfPathSegmentType.CubicTo, pointCount: 3);
        WritePoint(MapPoint(control1));
        WritePoint(MapPoint(control2));
        WritePoint(MapPoint(end));
    }

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    public void Close() => StartSegment(PdfPathSegmentType.Close, pointCount: 0);

    /// <summary>
    /// Appends another path's segments, transformed by this builder's matrix. An identity matrix leaves
    /// the segments untouched, so they are copied as raw bytes rather than walked point by point.
    /// </summary>
    public void AddPath(PdfPath other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        if (_isIdentityMatrix)
        {
            ReadOnlySpan<byte> otherBuffer = other.Buffer.Span;
            EnsureCapacity(_length + otherBuffer.Length);

            otherBuffer.CopyTo(_buffer.AsSpan(_length));
            _length += otherBuffer.Length;

            return;
        }

        foreach (PdfPathSegment segment in other.Segments)
        {
            ReadOnlySpan<PdfPoint> points = segment.Points;

            StartSegment(segment.Type, points.Length);

            for (int index = 0; index < points.Length; index++)
            {
                WritePoint(MapPoint(points[index]));
            }
        }
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

    /// <summary>
    /// Gives the accumulated segments to a new <see cref="PdfPath"/> with the given fill type and leaves
    /// this builder empty. The path takes the buffer over, so a builder used again after this starts a new
    /// one rather than writing into the path's.
    /// </summary>
    public PdfPath Detach(PdfPathFillType fillType = PdfPathFillType.Winding)
    {
        PdfPath path = new(_buffer.AsMemory(0, _length), fillType);

        _buffer = Array.Empty<byte>();
        _length = 0;

        return path;
    }

    /// <summary>
    /// <paramref name="point"/> transformed by this builder's matrix, or as it stands when that matrix is
    /// the identity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfPoint MapPoint(in PdfPoint point) => _isIdentityMatrix ? point : _matrix.MapPoint(point);

    /// <summary>
    /// Writes the type of a segment carrying <paramref name="pointCount"/> points and makes room for those
    /// points, which the caller writes with <see cref="WritePoint"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StartSegment(PdfPathSegmentType type, int pointCount)
    {
        EnsureCapacity(_length + 1 + (pointCount * Unsafe.SizeOf<PdfPoint>()));

        _buffer[_length] = (byte)type;
        _length++;
    }

    /// <summary>
    /// Writes <paramref name="point"/> at the end of the buffer, in the room the segment it belongs to has
    /// already reserved.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WritePoint(in PdfPoint point)
    {
        Unsafe.WriteUnaligned(ref _buffer[_length], point);
        _length += Unsafe.SizeOf<PdfPoint>();
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

using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Builds a glyph outline incrementally into a growable binary buffer, then detaches it as a
/// <see cref="ReadOnlyMemory{Byte}"/>: each segment is a one-byte <see cref="PdfFontPathSegmentType"/>
/// tag followed by its coordinates.
/// Reusable across many glyph outlines via <see cref="Reset"/>, which drops the accumulated
/// segments without releasing the buffer.
/// </summary>
public sealed class PdfFontPathBuilder
{
    private const int DefaultCapacity = 4;

    private readonly PdfFontMatrix _matrix;
    private readonly bool _isIdentityMatrix;

    private byte[] _buffer = [];
    private int _length;

    /// <summary>
    /// Initializes a new <see cref="PdfFontPathBuilder"/>. Every point passed to a segment method is
    /// transformed by <paramref name="matrix"/> before being stored -- pass
    /// <see cref="PdfFontMatrix.Identity"/> to store points unchanged.
    /// </summary>
    public PdfFontPathBuilder(in PdfFontMatrix matrix)
    {
        _matrix = matrix;
        _isIdentityMatrix = matrix.IsIdentity;
    }

    /// <summary>
    /// Whether no segments have been accumulated yet.
    /// </summary>
    public bool IsEmpty => _length == 0;

    /// <summary>
    /// Starts a new subpath at the given coordinates.
    /// </summary>
    public void MoveTo(float x, float y) => MoveTo(new PdfFontPoint(x, y));

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(in PdfFontPoint point)
    {
        StartSegment(PdfFontPathSegmentType.MoveTo, pointCount: 1);
        WritePoint(point);
    }

    /// <summary>
    /// Draws a straight line from the current point to the given coordinates.
    /// </summary>
    public void LineTo(float x, float y) => LineTo(new PdfFontPoint(x, y));

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(in PdfFontPoint point)
    {
        StartSegment(PdfFontPathSegmentType.LineTo, pointCount: 1);
        WritePoint(point);
    }

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(float x1, float y1, float x2, float y2, float x3, float y3)
        => CubicTo(new PdfFontPoint(x1, y1), new PdfFontPoint(x2, y2), new PdfFontPoint(x3, y3));

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(in PdfFontPoint control1, in PdfFontPoint control2, in PdfFontPoint end)
    {
        StartSegment(PdfFontPathSegmentType.CubicTo, pointCount: 3);
        WritePoint(control1);
        WritePoint(control2);
        WritePoint(end);
    }

    /// <summary>
    /// Draws a quadratic Bézier curve (TrueType's native curve form) from <paramref name="current"/>
    /// through one control point to an end point, by degree-elevating it to the equivalent cubic curve
    /// this format actually stores.
    /// </summary>
    public void QuadraticTo(in PdfFontPoint current, in PdfFontPoint control, in PdfFontPoint end)
    {
        const float twoThirds = 2f / 3f;

        PdfFontPoint control1 = new(
            current.X + (twoThirds * (control.X - current.X)),
            current.Y + (twoThirds * (control.Y - current.Y)));

        PdfFontPoint control2 = new(
            end.X + (twoThirds * (control.X - end.X)),
            end.Y + (twoThirds * (control.Y - end.Y)));

        CubicTo(control1, control2, end);
    }

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    public void Close() => StartSegment(PdfFontPathSegmentType.Close, pointCount: 0);

    /// <summary>
    /// Removes all accumulated segments so the builder can be reused. The underlying buffer is kept, not
    /// shrunk, so a builder reused for similarly sized paths does not repeatedly reallocate.
    /// </summary>
    public void Reset() => _length = 0;

    /// <summary>
    /// Gives the segments accumulated so far to the caller as a buffer, and leaves this builder empty.
    /// A builder used again after this starts a new path rather than continuing the returned one.
    /// </summary>
    public ReadOnlyMemory<byte> Detach()
    {
        ReadOnlyMemory<byte> detached = _buffer.AsMemory(0, _length);

        _buffer = Array.Empty<byte>();
        _length = 0;

        return detached;
    }

    /// <summary>
    /// Writes the type of a segment carrying <paramref name="pointCount"/> points and makes room for those
    /// points, which the caller writes with <see cref="WritePoint"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StartSegment(PdfFontPathSegmentType type, int pointCount)
    {
        EnsureCapacity(_length + 1 + (pointCount * Unsafe.SizeOf<PdfFontPoint>()));

        _buffer[_length] = (byte)type;
        _length++;
    }

    /// <summary>
    /// Writes <paramref name="point"/>, transformed by this builder's matrix, at the end of the
    /// buffer, in the room the segment it belongs to has already reserved.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WritePoint(in PdfFontPoint point)
    {
        Unsafe.WriteUnaligned(ref _buffer[_length], _isIdentityMatrix ? point : _matrix.MapPoint(point));
        _length += Unsafe.SizeOf<PdfFontPoint>();
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

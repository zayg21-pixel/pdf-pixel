using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Builds a glyph outline incrementally into a growable binary buffer, then detaches it as a
/// <see cref="ReadOnlyMemory{Byte}"/> in the encoded format <c>PdfPixel.Geometry.PdfPath</c> reads:
/// each segment is a one-byte <see cref="PdfFontPathSegmentType"/> tag followed by its coordinates.
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
    /// Initializes a new <see cref="PdfFontPathBuilder"/>. Every point passed to <see cref="MoveTo"/>,
    /// <see cref="LineTo"/>, and <see cref="CubicTo"/> is transformed by <paramref name="matrix"/> before
    /// being stored -- pass <see cref="PdfFontMatrix.Identity"/> to store points unchanged.
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
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(float x, float y)
    {
        StartSegment(PdfFontPathSegmentType.MoveTo, pointCount: 1);
        WritePoint(x, y);
    }

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(float x, float y)
    {
        StartSegment(PdfFontPathSegmentType.LineTo, pointCount: 1);
        WritePoint(x, y);
    }

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(float x1, float y1, float x2, float y2, float x3, float y3)
    {
        StartSegment(PdfFontPathSegmentType.CubicTo, pointCount: 3);
        WritePoint(x1, y1);
        WritePoint(x2, y2);
        WritePoint(x3, y3);
    }

    /// <summary>
    /// Draws a quadratic Bézier curve (TrueType's native curve form) from <paramref name="currentX"/>/
    /// <paramref name="currentY"/> through one control point to an end point, by degree-elevating it
    /// to the equivalent cubic curve this format actually stores.
    /// </summary>
    public void QuadraticTo(float currentX, float currentY, float controlX, float controlY, float endX, float endY)
    {
        const float twoThirds = 2f / 3f;
        float control1X = currentX + (twoThirds * (controlX - currentX));
        float control1Y = currentY + (twoThirds * (controlY - currentY));
        float control2X = endX + (twoThirds * (controlX - endX));
        float control2Y = endY + (twoThirds * (controlY - endY));
        CubicTo(control1X, control1Y, control2X, control2Y, endX, endY);
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
    /// Gives the segments accumulated so far to the caller as a buffer, in the format
    /// <c>PdfPixel.Geometry.PdfPath</c>'s constructor accepts directly, and leaves this builder empty.
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
        EnsureCapacity(_length + 1 + (pointCount * 2 * sizeof(float)));

        _buffer[_length] = (byte)type;
        _length++;
    }

    /// <summary>
    /// Writes <paramref name="x"/>/<paramref name="y"/>, transformed by this builder's matrix, at the end
    /// of the buffer, in the room the segment it belongs to has already reserved.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WritePoint(float x, float y)
    {
        if (!_isIdentityMatrix)
        {
            (x, y) = _matrix.MapPoint(x, y);
        }

        Unsafe.WriteUnaligned(ref _buffer[_length], x);
        _length += sizeof(float);
        Unsafe.WriteUnaligned(ref _buffer[_length], y);
        _length += sizeof(float);
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

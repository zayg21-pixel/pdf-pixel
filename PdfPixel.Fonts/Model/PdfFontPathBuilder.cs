using System;
using System.Runtime.InteropServices;

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
    private byte[] _buffer = [];
    private int _length;

    /// <summary>
    /// Initializes a new <see cref="PdfFontPathBuilder"/>. Every point passed to <see cref="MoveTo"/>,
    /// <see cref="LineTo"/>, and <see cref="CubicTo"/> is transformed by <paramref name="matrix"/> before
    /// being stored -- pass <see cref="PdfFontMatrix.Identity"/> to store points unchanged.
    /// </summary>
    public PdfFontPathBuilder(in PdfFontMatrix matrix) => _matrix = matrix;

    /// <summary>
    /// Whether no segments have been accumulated yet.
    /// </summary>
    public bool IsEmpty => _length == 0;

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(float x, float y)
    {
        (float mappedX, float mappedY) = _matrix.MapPoint(x, y);
        WriteSegment(PdfFontPathSegmentType.MoveTo, stackalloc float[] { mappedX, mappedY });
    }

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(float x, float y)
    {
        (float mappedX, float mappedY) = _matrix.MapPoint(x, y);
        WriteSegment(PdfFontPathSegmentType.LineTo, stackalloc float[] { mappedX, mappedY });
    }

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(float x1, float y1, float x2, float y2, float x3, float y3)
    {
        (float mappedX1, float mappedY1) = _matrix.MapPoint(x1, y1);
        (float mappedX2, float mappedY2) = _matrix.MapPoint(x2, y2);
        (float mappedX3, float mappedY3) = _matrix.MapPoint(x3, y3);
        WriteSegment(PdfFontPathSegmentType.CubicTo, stackalloc float[] { mappedX1, mappedY1, mappedX2, mappedY2, mappedX3, mappedY3 });
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
    public void Close() => WriteSegment(PdfFontPathSegmentType.Close, ReadOnlySpan<float>.Empty);

    /// <summary>
    /// Removes all accumulated segments so the builder can be reused. The underlying buffer is kept, not
    /// shrunk, so a builder reused for similarly sized paths does not repeatedly reallocate.
    /// </summary>
    public void Reset() => _length = 0;

    /// <summary>
    /// Detaches the segments accumulated so far as a new buffer, in the format
    /// <c>PdfPixel.Geometry.PdfPath</c>'s constructor accepts directly. The builder remains usable
    /// afterward; the returned buffer is unaffected by further building or a later <see cref="Reset"/>.
    /// </summary>
    public ReadOnlyMemory<byte> ToPath()
    {
        var snapshot = new byte[_length];
        Buffer.BlockCopy(_buffer, 0, snapshot, 0, _length);
        return snapshot;
    }

    private void WriteSegment(PdfFontPathSegmentType type, in ReadOnlySpan<float> coordinates)
    {
        ReadOnlySpan<byte> coordinateBytes = MemoryMarshal.AsBytes(coordinates);
        EnsureCapacity(_length + 1 + coordinateBytes.Length);

        _buffer[_length] = (byte)type;
        _length++;

        coordinateBytes.CopyTo(_buffer.AsSpan(_length));
        _length += coordinateBytes.Length;
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

using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// Reads segments in order from a <see cref="PdfPath"/>'s internal binary buffer.
/// </summary>
public ref struct PdfPathSegmentEnumerator
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _offset;
    private PdfPathSegmentType _currentType;
    private ReadOnlySpan<PdfPoint> _currentPoints;

    // RCS1231 disabled: buffer is stored into a field and returned to the caller via Current. An "in"
    // parameter's dereferenced value cannot be returned past the current call, so it must stay by-value.
#pragma warning disable RCS1231
    internal PdfPathSegmentEnumerator(ReadOnlySpan<byte> buffer)
#pragma warning restore RCS1231
    {
        _buffer = buffer;
        _offset = 0;
        _currentType = default;
        _currentPoints = default;
    }

    /// <summary>
    /// The segment at the current position of the enumerator.
    /// </summary>
    public readonly PdfPathSegment Current => new(_currentType, _currentPoints);

    /// <summary>
    /// Advances to the next segment. Returns false when the buffer is exhausted.
    /// </summary>
    public bool MoveNext()
    {
        if (_offset >= _buffer.Length)
        {
            return false;
        }

        _currentType = (PdfPathSegmentType)_buffer[_offset];
        _offset++;

        int pointCount = PdfPath.GetPointCount(_currentType);
        int payloadLength = pointCount * PdfPath.PointSizeBytes;
        _currentPoints = MemoryMarshal.Cast<byte, PdfPoint>(_buffer.Slice(_offset, payloadLength));
        _offset += payloadLength;

        return true;
    }
}

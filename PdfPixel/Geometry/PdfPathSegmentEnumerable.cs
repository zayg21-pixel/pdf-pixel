using System;

namespace PdfPixel.Geometry;

/// <summary>
/// Enumerates the segments encoded in a <see cref="PdfPath"/>'s internal binary buffer.
/// </summary>
public readonly ref struct PdfPathSegmentEnumerable
{
    private readonly ReadOnlySpan<byte> _buffer;

    // RCS1231 disabled: buffer is stored into a field and returned to the caller. An "in" parameter's
    // dereferenced value cannot be returned past the current call, so this must stay a by-value parameter.
#pragma warning disable RCS1231
    internal PdfPathSegmentEnumerable(ReadOnlySpan<byte> buffer) => _buffer = buffer;
#pragma warning restore RCS1231

    /// <summary>
    /// Returns an enumerator over the segments in this path.
    /// </summary>
    public PdfPathSegmentEnumerator GetEnumerator() => new(_buffer);
}

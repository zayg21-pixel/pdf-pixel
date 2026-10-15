using PdfPixel.Png.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Png.Streams;

/// <summary>
/// Forward-only stream reading the compressed image data of a PNG file across its IDAT or fdAT chunks.
/// </summary>
internal sealed class PngSegmentStream : Stream
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly List<PngDataSegment> _segments;

    private int _segmentIndex;
    private int _segmentPosition;

    public PngSegmentStream(in ReadOnlyMemory<byte> data, List<PngDataSegment> segments)
    {
        _data = data;
        _segments = segments;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalCopied = 0;

        while (count > 0 && _segmentIndex < _segments.Count)
        {
            PngDataSegment segment = _segments[_segmentIndex];
            int remaining = segment.Length - _segmentPosition;

            if (remaining <= 0)
            {
                _segmentIndex++;
                _segmentPosition = 0;
                continue;
            }

            int toCopy = Math.Min(remaining, count);
            _data.Span.Slice(segment.Offset + _segmentPosition, toCopy).CopyTo(buffer.AsSpan(offset, toCopy));

            _segmentPosition += toCopy;
            offset += toCopy;
            count -= toCopy;
            totalCopied += toCopy;
        }

        return totalCopied;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

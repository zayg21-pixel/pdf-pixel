using System;
using System.IO;

namespace PdfRender.Imaging.Jpx.Parsing;

/// <summary>
/// Lightweight span reader tailored for JPEG 2000 marker streams.
/// Provides big-endian reads and marker scanning without allocations.
/// </summary>
internal ref struct JpxSpanReader
{
    private const byte MarkerPrefix = 0xFF;

    private ReadOnlySpan<byte> _span;
    private int _index;

    public JpxSpanReader(ReadOnlySpan<byte> span)
    {
        _span = span;
        _index = 0;
    }

    public bool EndOfSpan => _index >= _span.Length;
    public int Remaining => _span.Length - _index;
    public int Position => _index;

    public byte ReadByte()
    {
        if (_index >= _span.Length)
        {
            throw new EndOfStreamException("End of span reached while reading byte.");
        }

        return _span[_index++];
    }

    public ushort ReadUInt16BE()
    {
        if (_index + 1 >= _span.Length)
        {
            throw new EndOfStreamException("End of span reached while reading UInt16.");
        }

        ushort value = (ushort)(_span[_index] << 8 | _span[_index + 1]);
        _index += 2;
        return value;
    }

    public uint ReadUInt32BE()
    {
        if (_index + 3 >= _span.Length)
        {
            throw new EndOfStreamException("End of span reached while reading UInt32.");
        }

        uint value = (uint)(_span[_index] << 24 | _span[_index + 1] << 16 | _span[_index + 2] << 8 | _span[_index + 3]);
        _index += 4;
        return value;
    }

    public ushort PeekUInt16BE()
    {
        if (_index + 1 >= _span.Length)
        {
            throw new EndOfStreamException("End of span reached while peeking UInt16.");
        }

        return (ushort)(_span[_index] << 8 | _span[_index + 1]);
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count < 0 || _index + count > _span.Length)
        {
            throw new EndOfStreamException($"Cannot read {count} bytes, only {Remaining} remaining.");
        }

        ReadOnlySpan<byte> slice = _span.Slice(_index, count);
        _index += count;
        return slice;
    }

    public void Skip(int count)
    {
        if (count < 0 || _index + count > _span.Length)
        {
            throw new EndOfStreamException($"Cannot skip {count} bytes, only {Remaining} remaining.");
        }

        _index += count;
    }

    public byte ReadToNextMarker()
    {
        while (_index < _span.Length)
        {
            byte b = _span[_index++];
            if (b == MarkerPrefix)
            {
                // Collapse any repeated 0xFF padding; the caller reads the marker byte next
                while (_index < _span.Length && _span[_index] == MarkerPrefix)
                {
                    _index++;
                }

                return MarkerPrefix;
            }
        }

        throw new EndOfStreamException("End of span reached while searching for next marker.");
    }

    public void SetPosition(int position)
    {
        if (position < 0 || position > _span.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position out of span bounds.");
        }

        _index = position;
    }
}
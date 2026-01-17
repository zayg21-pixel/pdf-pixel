using System;
using System.IO;

namespace PdfRender.Imaging.Jpg.Readers;

/// <summary>
/// Lightweight span reader tailored for JPEG marker streams.
/// Provides big-endian reads and marker scanning without allocations.
/// </summary>
internal ref struct JpgSpanReader
{
    private const byte MarkerPrefix = 0xFF;

    private ReadOnlySpan<byte> _span;
    private int _index;

    public JpgSpanReader(ReadOnlySpan<byte> span)
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
            throw new EndOfStreamException();
        }

        return _span[_index++];
    }

    public ushort ReadUInt16BE()
    {
        if (_index + 1 >= _span.Length)
        {
            throw new EndOfStreamException();
        }

        ushort v = (ushort)(_span[_index] << 8 | _span[_index + 1]);
        _index += 2;
        return v;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count < 0 || _index + count > _span.Length)
        {
            throw new EndOfStreamException();
        }

        var slice = _span.Slice(_index, count);
        _index += count;
        return slice;
    }

    public byte ReadToNextMarker()
    {
        while (_index < _span.Length)
        {
            byte b = _span[_index++];
            if (b == MarkerPrefix)
            {
                // collapse any repeated 0xFF padding; the caller reads the marker byte next
                while (_index < _span.Length && _span[_index] == MarkerPrefix)
                {
                    _index++;
                }

                return MarkerPrefix;
            }
        }

        throw new EndOfStreamException();
    }
}

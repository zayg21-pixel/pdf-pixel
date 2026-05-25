using System;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// JBIG2 Huffman decoder for segments that use Huffman coding instead of arithmetic coding.
/// Implements the standard and user-defined Huffman table decoding procedures per ITU-T T.88 Annex B.
/// </summary>
internal sealed class Jbig2HuffmanDecoder
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _byteOffset;
    private int _bitOffset; // bits consumed within current byte (0-7, MSB first)

    /// <summary>
    /// Initializes the Huffman decoder from the encoded data.
    /// </summary>
    /// <param name="data">Huffman-coded data.</param>
    public Jbig2HuffmanDecoder(in ReadOnlyMemory<byte> data)
    {
        _data = data;
        _byteOffset = 0;
        _bitOffset = 0;
    }

    /// <summary>
    /// Initializes the Huffman decoder from a span, copying to internal memory.
    /// </summary>
    /// <param name="data">Huffman-coded data span.</param>
    public Jbig2HuffmanDecoder(in ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
        _byteOffset = 0;
        _bitOffset = 0;
    }

    /// <summary>
    /// Reads a single bit from the stream (MSB-first).
    /// </summary>
    /// <returns>0 or 1.</returns>
    public int ReadBit()
    {
        ReadOnlySpan<byte> span = _data.Span;
        if (_byteOffset >= span.Length)
        {
            throw new InvalidOperationException(
                "JBIG2 Huffman stream exhausted: attempted to read past end of coded data.");
        }

        int bit = (span[_byteOffset] >> (7 - _bitOffset)) & 1;
        _bitOffset++;
        if (_bitOffset >= 8)
        {
            _bitOffset = 0;
            _byteOffset++;
        }

        return bit;
    }

    /// <summary>
    /// Reads N bits from the stream as an unsigned integer (MSB-first).
    /// </summary>
    /// <param name="count">Number of bits to read.</param>
    /// <returns>Unsigned integer value.</returns>
    public int ReadBits(int count)
    {
        int result = 0;
        for (int i = 0; i < count; i++)
        {
            result = (result << 1) | ReadBit();
        }

        return result;
    }

    /// <summary>
    /// Decodes a value from the given Huffman table.
    /// </summary>
    /// <param name="table">The Huffman table to decode with.</param>
    /// <returns>Decoded integer value, or int.MinValue for OOB.</returns>
    public int DecodeValue(Jbig2HuffmanTable table)
    {
        int code = 0;
        int codeLength = 0;

        for (int i = 0; i < table.Entries.Count; i++)
        {
            Jbig2HuffmanEntry entry = table.Entries[i];

            while (codeLength < entry.PrefixLength)
            {
                code = (code << 1) | ReadBit();
                codeLength++;
            }

            if (code == entry.PrefixCode)
            {
                if (entry.IsOob)
                {
                    return int.MinValue;
                }

                if (entry.RangeLength == 0)
                {
                    return entry.RangeLow;
                }

                int extraBits = ReadBits(entry.RangeLength);

                if (entry.IsLowerRange)
                {
                    return entry.RangeLow - extraBits;
                }

                return entry.RangeLow + extraBits;
            }
        }

        // No match found — stream is corrupted or table is invalid
        throw new InvalidOperationException(
            $"JBIG2 Huffman decode failed: no matching code found (code=0x{code:X}, length={codeLength}).");
    }

    /// <summary>
    /// Byte-aligns the bit position (skips remaining bits in the current byte).
    /// </summary>
    public void ByteAlign()
    {
        if (_bitOffset != 0)
        {
            _bitOffset = 0;
            _byteOffset++;
        }
    }

    /// <summary>
    /// Gets the current bit position in the stream.
    /// </summary>
    public int BitPosition => (_byteOffset * 8) + _bitOffset;

    /// <summary>
    /// Whether the decoder has consumed all available data.
    /// </summary>
    public bool IsExhausted => _byteOffset >= _data.Length;

    /// <summary>
    /// Gets the current byte position in the stream (byte-aligned).
    /// </summary>
    public int BytePosition => _byteOffset + ((_bitOffset > 0) ? 1 : 0);

    /// <summary>
    /// Sets the byte position directly (also resets the bit offset).
    /// </summary>
    public void SetBytePosition(int position)
    {
        _byteOffset = position;
        _bitOffset = 0;
    }

    /// <summary>
    /// Gets the underlying data as a read-only span for direct sub-slice access
    /// (e.g. embedded arithmetic-coded regions within the Huffman stream).
    /// </summary>
    public ReadOnlySpan<byte> GetDataSpan() => _data.Span;
}

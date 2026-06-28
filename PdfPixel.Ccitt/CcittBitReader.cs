using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Ccitt;

/// <summary>
/// Bit reader for CCITT fax streams (MSB-first within each byte).
/// Uses an MSB-aligned ulong reservoir for efficient bit extraction,
/// bulk-filling up to 8 bytes at a time to minimize per-bit overhead.
/// Exposes read state so callers can snapshot/restore between logical decode units (rows).
/// </summary>
public ref struct CcittBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _byteIndex;
    private ulong _buffer;
    private int _bufferedBits;

    /// <summary>
    /// Create a reader over the given data, resuming from a previously captured state.
    /// </summary>
    public CcittBitReader(ref readonly ReadOnlySpan<byte> data, int byteIndex, int bufferedBits, ulong buffer)
    {
        _data = data;
        _byteIndex = byteIndex;
        _bufferedBits = bufferedBits;
        _buffer = buffer;
    }

    /// <summary>
    /// Current byte index within the underlying data span.
    /// </summary>
    public readonly int ByteIndex => _byteIndex;

    /// <summary>
    /// Number of valid bits currently buffered.
    /// </summary>
    public readonly int BufferedBits => _bufferedBits;

    /// <summary>
    /// Current buffer value for state snapshot/restore.
    /// </summary>
    public readonly ulong Buffer => _buffer;

    /// <summary>
    /// Reads and consumes the next bit. Returns 0 or 1, or -1 at end of stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadBit()
    {
        if (_bufferedBits == 0)
        {
            if (_byteIndex >= _data.Length)
            {
                return -1;
            }

            FillBuffer();
        }

        var bit = (int)(_buffer >> 63);
        _buffer <<= 1;
        _bufferedBits--;
        return bit;
    }

    /// <summary>
    /// Peeks up to 24 bits without consuming them. Pads with zeros at end of stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PeekBits(int count)
    {
        if (_bufferedBits < count)
        {
            if (_byteIndex < _data.Length)
            {
                FillBuffer();
            }
        }

        if (_bufferedBits >= count)
        {
            return (int)(_buffer >> (64 - count));
        }

        if (_bufferedBits == 0)
        {
            return 0;
        }

        return (int)(_buffer >> (64 - count));
    }

    /// <summary>
    /// Drops (consumes) the specified number of previously peeked bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DropBits(int count)
    {
        _buffer <<= count;
        _bufferedBits -= count;
    }

    /// <summary>
    /// Discards remaining bits in the current byte when <paramref name="byteAlign"/> is true,
    /// aligning the stream to the next byte boundary.
    /// </summary>
    public void AlignAfterEndOfLine(bool byteAlign)
    {
        if (byteAlign)
        {
            int remainder = _bufferedBits & 7;
            if (remainder > 0)
            {
                _buffer <<= remainder;
                _bufferedBits -= remainder;
            }
        }
    }

    /// <summary>
    /// Attempts to consume a 12-bit EOL marker (0x001). Returns true and advances the stream if found.
    /// </summary>
    public bool TryConsumeEol()
    {
        if (PeekBits(12) == 0x001)
        {
            DropBits(12);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to consume six consecutive EOL markers (Return-To-Control). Resets the stream position on failure.
    /// </summary>
    public bool TryConsumeRtc()
    {
        int saveByteIndex = _byteIndex;
        int saveBufferedBits = _bufferedBits;
        ulong saveBuffer = _buffer;

        for (int eolCount = 0; eolCount < 6; eolCount++)
        {
            if (!TryConsumeEol())
            {
                _byteIndex = saveByteIndex;
                _bufferedBits = saveBufferedBits;
                _buffer = saveBuffer;
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FillBuffer()
    {
        int bytesAvailable = _data.Length - _byteIndex;
        int slotsAvailable = (64 - _bufferedBits) >> 3;
        int bytesToRead = Math.Min(bytesAvailable, slotsAvailable);

        if (bytesToRead >= 8)
        {
            _buffer = BinaryPrimitives.ReadUInt64BigEndian(_data.Slice(_byteIndex, 8));
            _byteIndex += 8;
            _bufferedBits = 64;
            return;
        }

        for (int i = 0; i < bytesToRead; i++)
        {
            _buffer |= (ulong)_data[_byteIndex] << (56 - _bufferedBits);
            _bufferedBits += 8;
            _byteIndex++;
        }
    }
}

using System;

namespace PdfPixel.Ccitt;

/// <summary>
/// Bit reader for CCITT fax streams (MSB-first within each byte).
/// Provides minimal helpers for optional EOL handling and alignment.
/// Exposes read state so callers can snapshot/restore between logical decode units (rows).
/// </summary>
public ref struct CcittBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _byteIndex;
    private int _bitsRemaining;
    private byte _current;
    private readonly bool _msbFirst;

    /// <summary>
    /// Create a reader with explicit state (useful for resuming mid-stream without keeping the ref struct alive).
    /// </summary>
    /// <param name="data">Encoded data span.</param>
    /// <param name="byteIndex">Current byte index within the data.</param>
    /// <param name="bitsRemaining">Bits remaining in the current working byte.</param>
    /// <param name="current">Current working byte (already shifted appropriately).</param>
    /// <param name="msbFirst">Bit order flag.</param>
    public CcittBitReader(ref readonly ReadOnlySpan<byte> data, int byteIndex, int bitsRemaining, byte current, bool msbFirst = true)
    {
        _data = data;
        _byteIndex = byteIndex;
        _bitsRemaining = bitsRemaining;
        _current = current;
        _msbFirst = msbFirst;
    }

    /// <summary>
    /// Current byte index within the underlying data span.
    /// </summary>
    public readonly int ByteIndex => _byteIndex;

    /// <summary>
    /// Remaining bit count in the working byte.
    /// </summary>
    public readonly int BitsRemaining => _bitsRemaining;

    /// <summary>
    /// Current shifted working byte value.
    /// </summary>
    public readonly byte Current => _current;

    /// <summary>
    /// Reads and consumes the next bit. Returns 0 or 1, or -1 at end of stream.
    /// </summary>
    public int ReadBit()
    {
        if (_bitsRemaining == 0)
        {
            if (_byteIndex >= _data.Length)
            {
                return -1;
            }

            _current = _data[_byteIndex++];
            _bitsRemaining = 8;
        }

        int bit;
        if (_msbFirst)
        {
            bit = _current >> 7 & 1;
            _current <<= 1;
        }
        else
        {
            bit = _current & 1;
            _current >>= 1;
        }

        _bitsRemaining--;
        return bit;
    }

    /// <summary>
    /// Discards remaining bits in the current byte when <paramref name="byteAlign"/> is true, aligning the stream to the next byte boundary.
    /// </summary>
    public void AlignAfterEndOfLine(bool byteAlign)
    {
        if (byteAlign)
        {
            _bitsRemaining = 0; // drop remainder
        }
    }

    /// <summary>
    /// Attempts to consume a 12-bit EOL marker (0x001). Returns true and advances the stream if found.
    /// </summary>
    public bool TryConsumeEol()
    {
        if (PeekBits(12) == 0x001)
        {
            AdvanceBits(12);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to consume six consecutive EOL markers (Return-To-Control). Resets the stream position on failure.
    /// </summary>
    public bool TryConsumeRtc()
    {
        // RTC = six consecutive EOLs (not frequently used in G4 but included for completeness)
        int saveByte = _byteIndex;
        int saveRemain = _bitsRemaining;
        byte saveCurrent = _current;

        for (int eolCount = 0; eolCount < 6; eolCount++)
        {
            if (!TryConsumeEol())
            {
                _byteIndex = saveByte;
                _bitsRemaining = saveRemain;
                _current = saveCurrent;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Peeks up to <paramref name="count"/> bits (1–24) without advancing the stream. Pads with zeros at end of stream.
    /// </summary>
    public int PeekBits(int count)
    {
        if (count <= 0 || count > 24)
        {
            return 0;
        }

        int saveByte = _byteIndex;
        int saveRemain = _bitsRemaining;
        byte saveCurrent = _current;
        int value = 0;

        for (int i = 0; i < count; i++)
        {
            int bit = ReadBit();
            if (bit < 0)
            {
                value <<= count - i; // pad with zeros
                break;
            }

            value = value << 1 | bit;
        }

        _byteIndex = saveByte;
        _bitsRemaining = saveRemain;
        _current = saveCurrent;
        return value;
    }

    private void AdvanceBits(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (ReadBit() < 0)
            {
                break;
            }
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Low-level JBIG2 QM-coder bitstream reader (ITU-T T.88 Annex E).
/// Context-adaptive binary arithmetic coding with probability estimation state machine.
/// Uses a merged 32-bit C register (high 16 bits = code value high, low 16 bits = code value low).
/// </summary>
internal ref struct Jbig2ArithmeticReader
{
    private readonly ReadOnlySpan<byte> _data;
#if NET5_0_OR_GREATER
    private readonly ref uint _qeTablePackedReferece;
#endif
    private readonly ReadOnlySpan<uint> _qeTablePacked; // Packed QE table for probability estimation (see BuildQeTablePacked)
    private int _bp;          // byte pointer (position of last consumed byte)
    private readonly int _dataEnd;
    private uint _c;          // C register: high 16 bits = code value high, low 16 bits = code value low
    private uint _a;          // A register (interval)
    private int _ct;          // count of available bits
    private byte _lastByte;   // last byte read from the stream (for 0xFF detection in ByteIn)
    private bool _markerFound; // true once a marker or end-of-data is encountered

    /// <summary>
    /// Initializes the arithmetic reader from the given encoded data span.
    /// Performs the INITDEC procedure per ITU-T T.88 E.3.5.
    /// </summary>
    /// <param name="data">Encoded arithmetic data.</param>
    public Jbig2ArithmeticReader(in ReadOnlySpan<byte> data)
    {
        _qeTablePacked = QeTablePacked.AsSpan();

#if NET5_0_OR_GREATER
        _qeTablePackedReferece = ref MemoryMarshal.GetReference(_qeTablePacked);
#endif

        _data = data;
        _bp = 0;
        _dataEnd = data.Length;

        // INITDEC
        _lastByte = (data.Length > 0) ? data[0] : (byte)0;
        _c = (uint)_lastByte << 16;

        ByteIn();

        _c <<= 7;
        _ct -= 7;
        _a = 0x8000;
    }

    /// <summary>
    /// Decodes a single bit using the given context state.
    /// Context packing: upper 7 bits = state index, bit 0 = MPS
    /// </summary>
    /// <param name="context">Reference to the packed context byte.</param>
    /// <returns>The decoded bit (0 or 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecodeBit(ref byte context)
    {
        // Unpack: bit 0 = MPS, upper 7 bits = state index
        int cxMps = context & 1;

        // One 32-bit load replaces three dependent struct-field loads through ref readonly.
        // Layout: bits 16-31 = qe (also serves as qeShifted's high half), bits 8-15 = nextMpsShifted,
        // bits 0-7 = nextLpsWithSwitch. Unsafe.Add bypasses the array bounds check — context >> 1
        // is bounded by [0, 46] by construction (state machine never produces values outside that).
#if NET5_0_OR_GREATER
        uint packed = Unsafe.Add(ref _qeTablePackedReferece, context >> 1);
#else
        uint packed = Unsafe.Add(ref MemoryMarshal.GetReference(_qeTablePacked), context >> 1);
#endif

        uint qeShifted = packed & 0xFFFF_0000u;          // qe in the high 16 bits == qe << 16, no shift needed
        uint qe = packed >> 16;
        uint a = _a - qe;

        int decision;
        int newContext;
        if (_c < qeShifted)
        {
            // LPS sub-interval selected (exchangeLps)
            if (a < qe)
            {
                // Conditional exchange — MPS is decoded
                decision = cxMps;
                newContext = (int)((packed >> 8) & 0xFFu) | cxMps;       // NextMpsShifted | cxMps
            }
            else
            {
                // LPS decoded
                decision = 1 ^ cxMps;
                newContext = (int)(packed & 0xFFu) ^ cxMps;              // NextLpsWithSwitch ^ cxMps
            }

            a = qe;
        }
        else
        {
            _c -= qeShifted;
            if ((a & 0x8000) != 0)
            {
                _a = a;
                return cxMps;
            }

            // MPS sub-interval, renorm needed (exchangeMps)
            if (a < qe)
            {
                // Conditional exchange — LPS decoded
                decision = 1 ^ cxMps;
                newContext = (int)(packed & 0xFFu) ^ cxMps;              // NextLpsWithSwitch ^ cxMps
            }
            else
            {
                // MPS decoded
                decision = cxMps;
                newContext = (int)((packed >> 8) & 0xFFu) | cxMps;       // NextMpsShifted | cxMps
            }
        }

        // Renormalize
        do
        {
            if (_ct == 0)
            {
                ByteIn();
            }

            a <<= 1;
            _c <<= 1;
            _ct--;
        }
        while ((a & 0x8000) == 0);

        _a = a;
        context = (byte)newContext;
        return decision;
    }

    /// <summary>
    /// Decodes an integer value using the IAID procedure (ITU-T T.88 6.4.10).
    /// </summary>
    /// <param name="contexts">Context array for IAID (size = 1 &lt;&lt; codeLength).</param>
    /// <param name="codeLength">Number of bits in the symbol ID code.</param>
    /// <returns>Decoded integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecodeIaid(in Span<byte> contexts, int codeLength)
    {
        int prev = 1;
        for (int i = 0; i < codeLength; i++)
        {
            int bit = DecodeBit(ref contexts[prev]);
            prev = (prev << 1) | bit;
        }

        return prev - (1 << codeLength);
    }

    /// <summary>
    /// Decodes an integer value using the standard integer decoding procedure (ITU-T T.88 6.4.6).
    /// Returns whether the decode succeeded (false means OOB - out of band).
    /// </summary>
    /// <param name="contexts">Context array for the integer coder.</param>
    /// <param name="value">The decoded integer value.</param>
    /// <returns>True if a value was decoded, false if OOB.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DecodeInteger(in Span<byte> contexts, out int value)
    {
        value = 0;
        int prev = 1;

        int sign = ReadIntBits(ref prev, contexts, 1);

        int bit1 = ReadIntBits(ref prev, contexts, 1);
        int result;
        if (bit1 != 0)
        {
            int bit2 = ReadIntBits(ref prev, contexts, 1);
            if (bit2 != 0)
            {
                int bit3 = ReadIntBits(ref prev, contexts, 1);
                if (bit3 != 0)
                {
                    int bit4 = ReadIntBits(ref prev, contexts, 1);
                    if (bit4 != 0)
                    {
                        int bit5 = ReadIntBits(ref prev, contexts, 1);
                        if (bit5 != 0)
                        {
                            result = ReadIntBits(ref prev, contexts, 32) + 4436;
                        }
                        else
                        {
                            result = ReadIntBits(ref prev, contexts, 12) + 340;
                        }
                    }
                    else
                    {
                        result = ReadIntBits(ref prev, contexts, 8) + 84;
                    }
                }
                else
                {
                    result = ReadIntBits(ref prev, contexts, 6) + 20;
                }
            }
            else
            {
                result = ReadIntBits(ref prev, contexts, 4) + 4;
            }
        }
        else
        {
            result = ReadIntBits(ref prev, contexts, 2);
        }

        if (sign == 0)
        {
            value = result;
        }
        else if (result > 0)
        {
            value = -result;
        }
        else
        {
            // sign=1 and value=0 → OOB
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads N bits for integer decoding with context index clamping.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadIntBits(ref int prev, in Span<byte> contexts, int count)
    {
        int v = 0;
        for (int i = 0; i < count; i++)
        {
            int bit = DecodeBit(ref contexts[prev]);
            prev = (prev < 256) ? (prev << 1) | bit : (((prev << 1) | bit) & 511) | 256;
            v = (v << 1) | bit;
        }

        return v;
    }

    /// <summary>
    /// BYTEIN procedure per ITU-T T.88 E.3.4.
    /// Reads the next byte into the C register. Handles 0xFF byte-stuffing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ByteIn()
    {
        if (_markerFound)
        {
            // Repeat 0xFF00 on every call — matches the reference which never advances
            // bp past a marker, so it keeps re-detecting the same 0xFF and adding 0xFF00.
            _c += 0xFF00u;
            _ct = 8;
            return;
        }

        if (_lastByte == 0xFF)
        {
            ByteInAfterFF();
            return;
        }

        _bp++;
        if (_bp < _dataEnd)
        {
            _lastByte = Unsafe.Add(ref MemoryMarshal.GetReference(_data), _bp);
            _c += (uint)_lastByte << 8;
        }
        else
        {
            // Past end of data — feed 0xFF stuffing.
            // Do NOT update _lastByte: keeping it at its current (non-0xFF) value ensures
            // the next call stays in the non-0xFF path and keeps adding 0xFF00 indefinitely,
            // matching the reference which never re-enters the 0xFF branch after the stream ends.
            _c += 0xFF00u;
        }

        _ct = 8;
    }

    /// <summary>
    /// Handles the BYTEIN case where the previous byte was 0xFF (bit-stuffing or marker).
    /// Separated to keep the common ByteIn path short and inlinable.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ByteInAfterFF()
    {
        int nextPos = _bp + 1;
        if (nextPos < _dataEnd
            && Unsafe.Add(ref MemoryMarshal.GetReference(_data), nextPos) <= 0x8F)
        {
            // Stuffed byte — consume it
            _bp = nextPos;
            _lastByte = Unsafe.Add(ref MemoryMarshal.GetReference(_data), _bp);
            _c += (uint)_lastByte << 9;
            _ct = 7;
        }
        else if (nextPos >= _dataEnd)
        {
            // End of data after 0xFF — treat as stuffed with 0, matching the reference where
            // an out-of-bounds access returns undefined, and undefined << 9 = 0 in JavaScript.
            _bp = nextPos;
            _lastByte = 0;
            _ct = 7;
        }
        else
        {
            // Actual marker (next byte > 0x8F within the data) — freeze with repeated 0xFF00.
            _markerFound = true;
            _c += 0xFF00u;
            _ct = 8;
        }
    }

    /// <summary>
    /// Standard QM-coder probability estimation table (47 entries) packed into a single uint
    /// per state. Layout per entry: bits 16-31 = Qe (16-bit probability, also acts as the
    /// pre-shifted qeShifted with no extra shift), bits 8-15 = nextMps &lt;&lt; 1 (state index
    /// pre-shifted into the context-byte slot), bits 0-7 = (nextLps &lt;&lt; 1) | switchInt.
    /// One memory load gives all four pieces; extractions become register operations.
    /// </summary>
    private static readonly uint[] QeTablePacked = BuildQeTablePacked();

    private static uint[] BuildQeTablePacked()
    {
        // (qe, nextMps, nextLps, switchOnLps)
        (ushort qe, byte nextMps, byte nextLps, bool sw)[] entries =
        [
            (0x5601,  1,  1, true),
            (0x3401,  2,  6, false),
            (0x1801,  3,  9, false),
            (0x0AC1,  4, 12, false),
            (0x0521,  5, 29, false),
            (0x0221, 38, 33, false),
            (0x5601,  7,  6, true),
            (0x5401,  8, 14, false),
            (0x4801,  9, 14, false),
            (0x3801, 10, 14, false),
            (0x3001, 11, 17, false),
            (0x2401, 12, 18, false),
            (0x1C01, 13, 20, false),
            (0x1601, 29, 21, false),
            (0x5601, 15, 14, true),
            (0x5401, 16, 14, false),
            (0x5101, 17, 15, false),
            (0x4801, 18, 16, false),
            (0x3801, 19, 17, false),
            (0x3401, 20, 18, false),
            (0x3001, 21, 19, false),
            (0x2801, 22, 19, false),
            (0x2401, 23, 20, false),
            (0x2201, 24, 21, false),
            (0x1C01, 25, 22, false),
            (0x1801, 26, 23, false),
            (0x1601, 27, 24, false),
            (0x1401, 28, 25, false),
            (0x1201, 29, 26, false),
            (0x1101, 30, 27, false),
            (0x0AC1, 31, 28, false),
            (0x09C1, 32, 29, false),
            (0x08A1, 33, 30, false),
            (0x0521, 34, 31, false),
            (0x0441, 35, 32, false),
            (0x02A1, 36, 33, false),
            (0x0221, 37, 34, false),
            (0x0141, 38, 35, false),
            (0x0111, 39, 36, false),
            (0x0085, 40, 37, false),
            (0x0049, 41, 38, false),
            (0x0025, 42, 39, false),
            (0x0015, 43, 40, false),
            (0x0009, 44, 41, false),
            (0x0005, 45, 42, false),
            (0x0001, 45, 43, false),
            (0x5601, 46, 46, false)
        ];

        var packed = new uint[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            (ushort qe, byte nextMps, byte nextLps, bool sw) = entries[i];
            packed[i] = ((uint)qe << 16)
                | ((uint)(nextMps << 1) << 8)
                | (uint)((nextLps << 1) | (sw ? 1 : 0));
        }

        return packed;
    }
}

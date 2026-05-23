using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// MQ (context-adaptive binary arithmetic) decoder as specified in ITU-T T.800 Annex C.
/// Used for entropy decoding of JPEG 2000 code-block bit-plane data.
/// </summary>
/// <remarks>
/// <para>
/// The MQ decoder maintains an interval [A, A+C) and refines it based on context-dependent
/// probability estimates. Each context tracks a most-probable symbol (MPS) and a probability
/// state index into the standard estimation table (Table C.3).
/// </para>
/// <para>
/// Optimizations over the reference implementation:
/// <list type="bullet">
///   <item>Packed context array (state in bits 0–5, MPS in bit 7) — single load per context.</item>
///   <item>Packed state table (uint per entry) — single load for Qe + transitions.</item>
///   <item>CLZ-based bulk renormalization on .NET 8+ — eliminates the renorm loop when CT has enough bits.</item>
/// </list>
/// </para>
/// </remarks>
internal ref struct JpxMqDecoder
{
    /// <summary>
    /// Number of probability estimation states defined in ITU-T T.800 Table C.3.
    /// </summary>
    internal const int StateCount = 47;

    /// <summary>
    /// Number of contexts used in JPEG 2000 code-block decoding (19 total per ITU-T T.800).
    /// Context 0: uniform, Context 1: RLC, Contexts 2-10: ZC (significance),
    /// Contexts 11-15: SC (sign), Contexts 16-18: MR (magnitude refinement).
    /// </summary>
    internal const int ContextCount = 19;

    private readonly ReadOnlySpan<byte> _data;
    private int _pos;
    private byte _lastByte;     // Last byte read (for 0xFF detection in ByteIn)
    private bool _markerFound;  // Whether a marker has been encountered

    // Decoder registers (ITU-T T.800 C.2, software convention)
    private uint _cRegister;   // C register (complement of code value)
    private uint _aRegister;   // A register (interval register)
    private int _ctCounter;    // CT counter (bits remaining before next byte fill)

    // Packed context array: bits 0–5 = state index (0-46), bit 7 = MPS (0 or 1).
    // Single byte per context — one load instead of two separate array accesses.
    // Stored as byte[] rather than Span<byte> so the JIT can treat the readonly reference
    // as invariant across field writes, avoiding repeated pointer extraction.
    private readonly Span<byte> _contexts;

    // State table built from ITU-T T.800 Table C.3.
    private readonly ReadOnlySpan<MqState> _states = BuildStates();

    /// <summary>
    /// Initializes the MQ decoder for the given code-block data.
    /// </summary>
    /// <param name="data">Entropy-coded code-block bytes.</param>
    public JpxMqDecoder(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;
        _contexts = new byte[ContextCount];

        // Initialize contexts per ITU-T T.800 Table D.7
        // Context 0 (uniform): state 46, MPS=0
        _contexts[0] = 46;
        // Context 1 (run-length): state 3, MPS=0
        _contexts[1] = 3;
        // Context 2 (first ZC): state 4, MPS=0
        _contexts[2] = 4;
        // All other contexts: state 0, MPS=0 (default from array init)

        // INITDEC procedure (ITU-T T.800 C.2.7)
        _aRegister = 0x8000;
        _lastByte = ReadByte();
        _cRegister = (uint)((_lastByte ^ 0xFF) << 16);
        ByteIn();
        _cRegister <<= 7;
        _ctCounter -= 7;
    }

    /// <summary>
    /// Gets the current state index for the given context.
    /// Exposed for testing.
    /// </summary>
    internal int GetContextState(int context)
    {
        return _contexts[context] & 0x3F;
    }

    /// <summary>
    /// Gets the current MPS value for the given context.
    /// Exposed for testing.
    /// </summary>
    internal int GetContextMps(int context)
    {
        return _contexts[context] >> 7;
    }

    /// <summary>
    /// Decodes one binary decision for the given context label.
    /// </summary>
    /// <param name="context">Context index (0 to <see cref="ContextCount"/> - 1).</param>
    /// <returns>The decoded symbol (0 or 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecodeBit(int context)
    {
        // Direct array element ref — the JIT knows _contexts (readonly field) is invariant
        // across writes to _aRegister/_cRegister, so the array base stays in a register.
        ref byte ctxRef = ref _contexts[context];
        byte ctx = ctxRef;
        int stateIndex = ctx & 0x3F;

        // Keep MPS in bit-7 position to avoid re-shifting on every write-back.
        // Only shift down to 0/1 once, for the return value.
        int mpsPacked = ctx & 0x80;
        int mps = mpsPacked >> 7;

        // static readonly field — JIT treats the base address as invariant, single indexed load.
        ref readonly MqState state = ref _states[stateIndex];

        // Subtract Qe from A
        _aRegister -= state.Qe;

        uint cHigh = _cRegister >> 16;

        if (cHigh < _aRegister)
        {
            // MPS sub-interval is selected
            if (_aRegister >= 0x8000)
            {
                // No renormalization needed — fast path (most common case)
                return mps;
            }

            // Conditional exchange check
            if (_aRegister < state.Qe)
            {
                // LPS due to conditional exchange; NextLpsWithSwitch has SwitchBit pre-OR'd into bit 7
                ctxRef = (byte)(state.NextLpsWithSwitch ^ mpsPacked);
                Renormalize();
                return 1 - mps;
            }
            else
            {
                // MPS, needs renorm
                ctxRef = (byte)(state.NextMps | mpsPacked);
                Renormalize();
                return mps;
            }
        }
        else
        {
            // LPS sub-interval
            _cRegister -= _aRegister << 16;

            // Conditional exchange check
            if (_aRegister < state.Qe)
            {
                // MPS due to conditional exchange
                ctxRef = (byte)(state.NextMps | mpsPacked);
                _aRegister = state.Qe;
                Renormalize();
                return mps;
            }
            else
            {
                // LPS
                ctxRef = (byte)(state.NextLpsWithSwitch ^ mpsPacked);
                _aRegister = state.Qe;
                Renormalize();
                return 1 - mps;
            }
        }
    }

    /// <summary>
    /// Resets all context states to their initial values per ITU-T T.800 Table D.7.
    /// Used when the OPT_RESET_MQ coding style flag is set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _contexts.Clear();
        _contexts[0] = 46; // uniform
        _contexts[1] = 3;  // RLC
        _contexts[2] = 4;  // first ZC
    }

    /// <summary>
    /// Renormalizes the A and C registers after a coding decision.
    /// Shifts A and C left one bit at a time until A >= 0x8000, refilling
    /// the bit buffer via ByteIn when exhausted.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Renormalize()
    {
        do
        {
            if (_ctCounter == 0)
            {
                ByteIn();
            }

            _aRegister <<= 1;
            _cRegister <<= 1;
            _ctCounter--;
        } while (_aRegister < 0x8000);
    }

    /// <summary>
    /// BYTEIN procedure per ITU-T T.800 C.2.4.
    /// Reads the next byte from the compressed data into the C register.
    /// Handles the 0xFF byte-stuffing convention.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ByteIn()
    {
        if (_markerFound)
        {
            _ctCounter = 8;
            return;
        }

        if (_lastByte == 0xFF)
        {
            ByteInAfterFF();
            return;
        }

        if (_pos >= _data.Length)
        {
            // Past end of data — feed 0xFF (produces zeros in complement convention)
            _lastByte = 0xFF;
            _ctCounter = 8;
            return;
        }

        byte currentByte = Unsafe.Add(ref MemoryMarshal.GetReference(_data), _pos);
        _pos++;
        _lastByte = currentByte;
        _cRegister += (uint)(0xFF00 - (currentByte << 8));
        _ctCounter = 8;
    }

    /// <summary>
    /// Handles the BYTEIN case where the previous byte was 0xFF (bit-stuffing).
    /// Separated to keep the common ByteIn path short and inlinable.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ByteInAfterFF()
    {
        if (_pos >= _data.Length)
        {
            _markerFound = true;
            _ctCounter = 8;
            return;
        }

        byte currentByte = Unsafe.Add(ref MemoryMarshal.GetReference(_data), _pos);

        if (currentByte > 0x8F)
        {
            // Marker found — don't consume
            _markerFound = true;
            _ctCounter = 8;
            return;
        }

        _pos++;
        _lastByte = currentByte;
        _cRegister += (uint)(0xFE00 - (currentByte << 9));
        _ctCounter = 7;
    }

    /// <summary>
    /// Reads a single byte from the data stream.
    /// Returns 0xFF if past end of data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadByte()
    {
        if (_pos >= _data.Length)
        {
            return 0xFF;
        }

        return Unsafe.Add(ref MemoryMarshal.GetReference(_data), _pos++);
    }


/// <summary>
/// Builds the state table from ITU-T T.800 Table C.3.
/// </summary>
    private static MqState[] BuildStates()
    {
        ReadOnlySpan<ushort> qeValues =
        [
            0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221,
            0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401,
            0x1C01, 0x1601, 0x5601, 0x5401, 0x5101, 0x4801,
            0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201,
            0x1C01, 0x1801, 0x1601, 0x1401, 0x1201, 0x1101,
            0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1,
            0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
            0x0015, 0x0009, 0x0005, 0x0001, 0x5601
        ];

        ReadOnlySpan<byte> nextMps =
        [
             1,  2,  3,  4,  5, 38,
             7,  8,  9, 10, 11, 12,
            13, 29, 15, 16, 17, 18,
            19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30,
            31, 32, 33, 34, 35, 36,
            37, 38, 39, 40, 41, 42,
            43, 44, 45, 45, 46
        ];

        ReadOnlySpan<byte> nextLps =
        [
             1,  6,  9, 12, 29, 33,
             6, 14, 14, 14, 17, 18,
            20, 21, 14, 14, 15, 16,
            17, 18, 19, 19, 20, 21,
            22, 23, 24, 25, 26, 27,
            28, 29, 30, 31, 32, 33,
            34, 35, 36, 37, 38, 39,
            40, 41, 42, 43, 46
        ];

        ReadOnlySpan<byte> switchBits =
        [
            1, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0,
            0, 0, 1, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0
        ];

        var states = new MqState[StateCount];

        for (int i = 0; i < StateCount; i++)
        {
            states[i] = new MqState(qeValues[i], nextMps[i], (byte)(nextLps[i] | (switchBits[i] << 7)));
        }

        return states;
    }
}

/// <summary>
/// Represents a single row of the MQ probability estimation table (ITU-T T.800 Table C.3).
/// </summary>
internal readonly struct MqState
{
    /// <summary>Gets the Qe probability estimate for this state.</summary>
    public readonly ushort Qe;

    /// <summary>Gets the next state index when the MPS (most probable symbol) is decoded.</summary>
    public readonly byte NextMps;

    /// <summary>
    /// Gets the next-state index for an LPS event with the switch bit pre-OR'd into bit 7.
    /// Bit 7 is set when the MPS sense must be flipped on an LPS event (ITU-T T.800 Table C.3 SWITCH column).
    /// Combining both values into one byte lets <c>DecodeBit</c> compute the new context as a single XOR
    /// against the packed MPS byte, instead of a separate load, shift, and OR.
    /// </summary>
    public readonly byte NextLpsWithSwitch;

    /// <summary>
    /// Initializes a new instance of <see cref="MqState"/>.
    /// </summary>
    /// <param name="qe">Qe probability estimate.</param>
    /// <param name="nextMps">Next state index for an MPS event.</param>
    /// <param name="nextLpsWithSwitch">Next LPS state index OR'd with the switch bit in bit 7.</param>
    public MqState(ushort qe, byte nextMps, byte nextLpsWithSwitch)
    {
        Qe = qe;
        NextMps = nextMps;
        NextLpsWithSwitch = nextLpsWithSwitch;
    }
}

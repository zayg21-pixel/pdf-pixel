using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// MQ (context-adaptive binary arithmetic) decoder as specified in ITU-T T.800 Annex C.
/// Used for entropy decoding of JPEG 2000 code-block bit-plane data.
/// </summary>
/// <remarks>
/// The MQ decoder maintains an interval [A, A+C) and refines it based on context-dependent
/// probability estimates. Each context tracks a most-probable symbol (MPS) and a probability
/// state index into the standard estimation table (Table C.3).
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

    // Qe values for each state (ITU-T T.800 Table C.3)
    private static readonly ushort[] QeValues =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521,
        0x0221, 0x5601, 0x5401, 0x4801, 0x3801,
        0x3001, 0x2401, 0x1C01, 0x1601, 0x5601,
        0x5401, 0x5101, 0x4801, 0x3801, 0x3401,
        0x3001, 0x2801, 0x2401, 0x2201, 0x1C01,
        0x1801, 0x1601, 0x1401, 0x1201, 0x1101,
        0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441,
        0x02A1, 0x0221, 0x0141, 0x0111, 0x0085,
        0x0049, 0x0025, 0x0015, 0x0009, 0x0005,
        0x0001, 0x5601
    ];

    // Next state after MPS (most probable symbol) renormalization
    private static readonly byte[] NextMps =
    [
         1,  2,  3,  4,  5, 38,  7,  8,  9, 10,
        11, 12, 13, 29, 15, 16, 17, 18, 19, 20,
        21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
        31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
        41, 42, 43, 44, 45, 45, 46
    ];

    // Next state after LPS (least probable symbol) renormalization
    private static readonly byte[] NextLps =
    [
         1,  6,  9, 12, 29, 33,  6, 14, 14, 14,
        17, 18, 20, 21, 14, 14, 15, 16, 17, 18,
        19, 19, 20, 21, 22, 23, 24, 25, 26, 27,
        28, 29, 30, 31, 32, 33, 34, 35, 36, 37,
        38, 39, 40, 41, 42, 43, 46
    ];

    // Whether MPS/LPS switch occurs on LPS renormalization
    private static readonly bool[] MpsLpsSwitch =
    [
        true,  false, false, false, false, false, true,  false, false, false,
        false, false, false, false, true,  false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false
    ];

    private readonly ReadOnlySpan<byte> _data;
    private int _pos;
    private byte _lastByte;     // Last byte read (for 0xFF detection in ByteIn)
    private bool _markerFound;  // Whether a marker has been encountered

    // Decoder registers (ITU-T T.800 C.2, software convention)
    private uint _cRegister;   // C register (complement of code value)
    private uint _aRegister;   // A register (interval register)
    private int _ctCounter;    // CT counter (bits remaining before next byte fill)

    // Context states
    private readonly Span<byte> _contextStates;  // State index per context
    private readonly Span<byte> _contextMps;     // MPS value per context (0 or 1)

    /// <summary>
    /// Initializes the MQ decoder for the given code-block data.
    /// </summary>
    /// <param name="data">Entropy-coded code-block bytes.</param>
    public JpxMqDecoder(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;

        _contextStates = new byte[ContextCount];
        _contextMps = new byte[ContextCount];

        // Initialize contexts per ITU-T T.800 Table D.7
        // Matches CoreJ2K MQ_INIT: { 46, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
        // Context 0 (uniform): state 46, MPS=0
        _contextStates[0] = 46;
        _contextMps[0] = 0;

        // Context 1 (run-length): state 3, MPS=0
        _contextStates[1] = 3;
        _contextMps[1] = 0;

        // Context 2 (first ZC - all-zero neighbourhood): state 4, MPS=0
        _contextStates[2] = 4;
        _contextMps[2] = 0;

        // All other contexts start at state 0, MPS=0 (default from array init)

        // INITDEC procedure (ITU-T T.800 C.2.7)
        InitializeDecoder();
    }

    /// <summary>
    /// Gets the Qe (probability) value for the given state index.
    /// Exposed for testing.
    /// </summary>
    internal static ushort GetQe(int stateIndex) => QeValues[stateIndex];

    /// <summary>
    /// Gets the current state index for the given context.
    /// Exposed for testing.
    /// </summary>
    internal int GetContextState(int context) => _contextStates[context];

    /// <summary>
    /// Gets the current MPS value for the given context.
    /// Exposed for testing.
    /// </summary>
    internal int GetContextMps(int context) => _contextMps[context];

    /// <summary>
    /// INITDEC procedure per ITU-T T.800 C.2.7.
    /// Initializes the C and A registers from the first bytes of data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeDecoder()
    {
        // Set A to 0x8000
        _aRegister = 0x8000;

        // Software-convention decoder: C stores complement of code value.
        // First byte is XORed with 0xFF per JJ2000/CoreJ2K convention.
        _lastByte = ReadByte();
        _cRegister = (uint)((_lastByte ^ 0xFF) << 16);

        // BYTEIN to fill more bits
        ByteIn();

        // Shift C left by 7 and decrement CT
        _cRegister <<= 7;
        _ctCounter -= 7;
    }

    /// <summary>
    /// Decodes one binary decision for the given context label.
    /// </summary>
    /// <param name="context">Context index (0 to <see cref="ContextCount"/> - 1).</param>
    /// <returns>The decoded symbol (0 or 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecodeBit(int context)
    {
        int stateIndex = _contextStates[context];
        uint qe = QeValues[stateIndex];
        int mps = _contextMps[context];

        // Subtract Qe from A
        _aRegister -= qe;

        int decision;

        if ((_cRegister >> 16) < _aRegister)
        {
            // MPS sub-interval is selected
            if (_aRegister >= 0x8000)
            {
                // No renormalization needed
                return mps;
            }

            // Conditional exchange check
            if (_aRegister < qe)
            {
                decision = 1 - mps; // LPS
                UpdateLps(context, stateIndex);
            }
            else
            {
                decision = mps; // MPS
                _contextStates[context] = NextMps[stateIndex];
            }
        }
        else
        {
            // LPS sub-interval
            // Remove interval from C
            _cRegister -= (_aRegister << 16);

            // Conditional exchange check
            if (_aRegister < qe)
            {
                decision = mps; // MPS due to exchange
                _contextStates[context] = NextMps[stateIndex];
            }
            else
            {
                decision = 1 - mps; // LPS
                UpdateLps(context, stateIndex);
            }

            _aRegister = qe;
        }

        // RENORMD
        Renormalize();

        return decision;
    }

    /// <summary>
    /// Resets all context states to their initial values per ITU-T T.800 Table D.7.
    /// Used when the OPT_RESET_MQ coding style flag is set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        // Reset all contexts to state 0, MPS=0
        _contextStates.Clear();
        _contextMps.Clear();

        // Re-initialize special contexts
        _contextStates[0] = 46; // uniform
        _contextStates[1] = 3;  // RLC
        _contextStates[2] = 4;  // first ZC
    }

    /// <summary>
    /// Decodes a single bit using the uniform (bypass) context.
    /// Used for raw coding passes.
    /// </summary>
    /// <returns>The decoded bit (0 or 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecodeBypass()
    {
        // Simplified decoder for uniform distribution (context 0, state 46)
        return DecodeBit(0);
    }

    /// <summary>
    /// Updates context state after LPS event.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLps(int context, int stateIndex)
    {
        if (MpsLpsSwitch[stateIndex])
        {
            _contextMps[context] = (byte)(1 - _contextMps[context]);
        }

        _contextStates[context] = NextLps[stateIndex];
    }

    /// <summary>
    /// RENORMD procedure per ITU-T T.800 C.2.5.
    /// Renormalizes the A and C registers after a decode operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        }
        while (_aRegister < 0x8000);
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
            // After marker, just set CT=8 (C unchanged)
            _ctCounter = 8;
            return;
        }

        if (_lastByte == 0xFF)
        {
            if (_pos >= _data.Length)
            {
                _markerFound = true;
                _ctCounter = 8;
                return;
            }

            byte currentByte = _data[_pos];

            if (currentByte > 0x8F)
            {
                // Marker found - don't consume, C unchanged
                _markerFound = true;
                _ctCounter = 8;
                return;
            }

            _pos++;
            _lastByte = currentByte;
            _cRegister += (uint)(0xFE00 - (currentByte << 9));
            _ctCounter = 7;
        }
        else
        {
            if (_pos >= _data.Length)
            {
                _lastByte = 0xFF;
                _cRegister += 0xFF00 - (0xFF << 8); // adds 0
                _ctCounter = 8;
                return;
            }

            byte currentByte = _data[_pos];
            _pos++;
            _lastByte = currentByte;
            _cRegister += (uint)(0xFF00 - (currentByte << 8));
            _ctCounter = 8;
        }
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

        return _data[_pos++];
    }
}

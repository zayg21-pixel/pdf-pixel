using PdfPixel.Imaging.Jpx.Model;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// EBCOT Tier-1 code-block decoder per ITU-T T.800 Annex D.
/// Decodes entropy-coded code-block data into wavelet coefficients using three coding pass types:
/// significance propagation, magnitude refinement, and cleanup.
/// </summary>
/// <remarks>
/// <para>
/// Each code-block is decoded independently. The decoder maintains per-sample state flags
/// that track significance, sign, and refinement status. Coding passes are processed in order
/// (cleanup first at MSB, then significance/magnitude/cleanup cycling) building up the
/// coefficient magnitudes bit-plane by bit-plane.
/// </para>
/// <para>
/// Bit layout of each <see cref="uint"/> state entry:
/// <list type="table">
///   <listheader><term>Bits</term><description>Meaning</description></listheader>
///   <item><term>0</term><description>FlagSignificant – sample has become significant</description></item>
///   <item><term>1</term><description>FlagSign – sign bit (0 = positive, 1 = negative)</description></item>
///   <item><term>2</term><description>FlagCoded – sample was coded in the current bit-plane</description></item>
///   <item><term>3</term><description>FlagRefined – sample has been refined at least once</description></item>
///   <item><term>4–11</term><description>Neighbor significance bits (left, right, top, bottom, TL, TR, BL, BR)</description></item>
/// </list>
/// </para>
/// </remarks>
internal ref partial struct JpxTier1Decoder
{
    // --- Sample flag bits (bits 0–3) ---

    /// <summary>Sample has become significant (magnitude > 0).</summary>
    private const uint FlagSignificant = 0x001;

    /// <summary>Sign bit: 0 = positive, 1 = negative.</summary>
    private const uint FlagSign = 0x002;

    /// <summary>Sample was coded in the current bit-plane (visited flag).</summary>
    private const uint FlagCoded = 0x004;

    /// <summary>Sample has been refined at least once (for magnitude refinement context).</summary>
    private const uint FlagRefined = 0x008;

    // --- Neighbor significance bits (bits 4–11) ---

    /// <summary>Left neighbor is significant (bit 4).</summary>
    private const uint NeighborLeft = 0x010;

    /// <summary>Right neighbor is significant (bit 5).</summary>
    private const uint NeighborRight = 0x020;

    /// <summary>Top neighbor is significant (bit 6).</summary>
    private const uint NeighborTop = 0x040;

    /// <summary>Bottom neighbor is significant (bit 7).</summary>
    private const uint NeighborBottom = 0x080;

    /// <summary>Top-left neighbor is significant (bit 8).</summary>
    private const uint NeighborTopLeft = 0x100;

    /// <summary>Top-right neighbor is significant (bit 9).</summary>
    private const uint NeighborTopRight = 0x200;

    /// <summary>Bottom-left neighbor is significant (bit 10).</summary>
    private const uint NeighborBottomLeft = 0x400;

    /// <summary>Bottom-right neighbor is significant (bit 11).</summary>
    private const uint NeighborBottomRight = 0x800;

    /// <summary>Mask covering all 8 neighbor significance bits (bits 4–11).</summary>
    private const uint NeighborMask = 0xFF0;

    /// <summary>Combined mask for significance-or-coded quick tests.</summary>
    private const uint FlagSignificantOrCoded = FlagSignificant | FlagCoded;

    /// <summary>
    /// Combined mask for run-length eligibility: sample must be insignificant,
    /// uncoded, and have no significant neighbours.
    /// </summary>
    private const uint RunLengthCheckMask = FlagSignificant | FlagCoded | NeighborMask;

    // --- Context labels (ITU-T T.800 Table D.1 / D.2) ---

    /// <summary>Context label for the uniform context (context 0).</summary>
    private const int ContextUniform = 0;

    /// <summary>Context label for the run-length context (context 1).</summary>
    private const int ContextRunLength = 1;

    /// <summary>Offset added to zero-coding (ZC) context values (0-8) to get context labels 2-10.</summary>
    private const int ContextZcOffset = 2;

    /// <summary>Offset added to sign-coding (SC) context values (0-4) to get context labels 11-15.</summary>
    private const int ContextScOffset = 11;

    /// <summary>Offset added to magnitude-refinement (MR) context values (0-2) to get context labels 16-18.</summary>
    private const int ContextMrOffset = 16;

    /// <summary>
    /// Bit position of the most significant magnitude bit in the sign-magnitude coefficient format.
    /// Bit 31 is the sign bit; bit 30 is the MSB of magnitude.
    /// </summary>
    internal const int MagnitudeBitPosition = 30;

    // Subband orientation constants
    private const int OrientLL = 0;
    private const int OrientHL = 1;
    private const int OrientLH = 2;
    private const int OrientHH = 3;

    /// <summary>
    /// Vertically-causal mask applied to the neighbor byte (after shifting bits 4–11 → 0–7).
    /// Clears bottom(3), bottom-left(6), bottom-right(7) in the shifted representation.
    /// </summary>
    private const int VcNeighborMask = 0x37;

    /// <summary>
    /// Precomputed significance context lookup table.
    /// Indexed by [orientation * 256 + neighborMask] where neighborMask is bits 4–11
    /// of the state word, shifted right by 4.
    /// </summary>
    private static readonly byte[] SignificanceContextLutArray = BuildSignificanceContextLut();

    /// <summary>
    /// Sign coding context label offsets (from ContextScOffset), indexed by combined + 4.
    /// combined = horizontalContribution * 3 + verticalContribution, range [-4, +4].
    /// </summary>
    private static readonly byte[] SignContextLabelArray = [4, 3, 2, 1, 0, 1, 2, 3, 4];

    /// <summary>
    /// Sign coding XOR bits, indexed by combined + 4.
    /// </summary>
    private static readonly byte[] SignContextXorBitArray = [1, 1, 1, 1, 0, 0, 0, 0, 0];

    // --- Instance fields ---

    private readonly ReadOnlySpan<byte> _significanceContextLut;
    private readonly ReadOnlySpan<byte> _signContextLabel;
    private readonly ReadOnlySpan<byte> _signContextXorBit;

    private readonly int _width;
    private readonly int _height;
    private readonly int _stateWidth;
    private readonly int _stateStripeStride;
    private readonly int _coeffStripeStride;
    private readonly int _stateLength;
    private readonly int _orientation;
    private readonly int _totalPasses;
    private readonly int _startBitPlane;
    private readonly bool _resetContexts;
    private readonly bool _terminateOnPass;
    private readonly bool _verticallyCausal;
    private readonly bool _segmentationSymbols;

    private readonly int[] _coefficients;
    private readonly uint[] _state;
    private JpxMqDecoder _mqDecoder;

    /// <summary>
    /// Creates a Tier-1 decoder configured for a specific code-block.
    /// All context is initialized once; call <see cref="Decode"/> to run the decoding pipeline.
    /// </summary>
    /// <param name="codingStyle">Coding style parameters from the codestream header.</param>
    /// <param name="codeBlock">The code-block containing compressed data and metadata.</param>
    public JpxTier1Decoder(JpxCodingStyle codingStyle, JpxCodeBlock codeBlock)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }

        _resetContexts = (codingStyle.CodeBlockStyle & 0x02) != 0;
        _terminateOnPass = (codingStyle.CodeBlockStyle & 0x04) != 0;
        _verticallyCausal = (codingStyle.CodeBlockStyle & 0x08) != 0;
        _segmentationSymbols = (codingStyle.CodeBlockStyle & 0x20) != 0;

        _width = codeBlock.Width;
        _height = codeBlock.Height;
        _stateWidth = _width + 2;
        _stateStripeStride = _stateWidth << 2;
        _coeffStripeStride = _width << 2;
        int stateHeight = _height + 2;
        _stateLength = stateHeight * _stateWidth;
        _orientation = GetOrientation(codeBlock);
        _totalPasses = codeBlock.CodingPasses;
        _startBitPlane = MagnitudeBitPosition - codeBlock.ZeroBitPlanes;

        // Capture static LUTs as ReadOnlySpan
        _significanceContextLut = SignificanceContextLutArray;
        _signContextLabel = SignContextLabelArray;
        _signContextXorBit = SignContextXorBitArray;

        // Allocate working buffers
        _coefficients = new int[_height * _width];
        _state = ArrayPool<uint>.Shared.Rent(_stateLength);
        Array.Clear(_state, 0, _stateLength);

        // Initialize MQ decoder from code-block data
        _mqDecoder = new JpxMqDecoder(codeBlock.Data.Span);
    }

    /// <summary>
    /// Decodes the code-block's entropy-coded data into wavelet coefficients.
    /// </summary>
    /// <returns>
    /// A flat array of wavelet coefficients [height * width] in row-major sign-magnitude format at bit 30.
    /// </returns>
    public int[] Decode()
    {
        if (_width <= 0 || _height <= 0 || _totalPasses == 0)
        {
            ArrayPool<uint>.Shared.Return(_state);
            return _coefficients;
        }

        int currentBitPlane = _startBitPlane;
        int currentPass = 0;
        int height = _height;

        while (currentPass < _totalPasses)
        {
            int passInSequence;

            if (currentPass == 0)
            {
                // First pass is always cleanup of the first coded bit-plane
                passInSequence = 2;
            }
            else
            {
                // After the first cleanup, passes cycle: sig-prop(0), mag-ref(1), cleanup(2)
                passInSequence = (currentPass - 1) % 3;
            }

            // Clear coded flags at the start of each bit-plane group
            if (passInSequence == 0 || currentPass == 0)
            {
                ClearCodedFlags();
            }

            // Initialize stripe refs at the first stripe position.
            // State: row 1 (skip border), col 1 (skip border) → offset = stateWidth + 1
            // Coeff: row 0, col 0 → offset = 0
            ref uint stripeStatePtr = ref Unsafe.Add(ref _state[0], _stateWidth + 1);
            ref int stripeCoeffPtr = ref _coefficients[0];

            for (int stripeTop = 0; stripeTop < height; stripeTop += 4)
            {
                int stripeBottom = Math.Min(stripeTop + 4, height);
                int stripeHeight = stripeBottom - stripeTop;

                switch (passInSequence)
                {
                    case 0:
                        ExecuteSignificancePropagation(ref stripeStatePtr, ref stripeCoeffPtr,
                            stripeHeight, currentBitPlane);
                        break;

                    case 1:
                        ExecuteMagnitudeRefinement(ref stripeStatePtr, ref stripeCoeffPtr,
                            stripeHeight, currentBitPlane);
                        break;

                    case 2:
                        ExecuteCleanup(ref stripeStatePtr, ref stripeCoeffPtr,
                            stripeTop, stripeHeight, currentBitPlane);
                        break;
                }

                // Advance stripe refs forward by 4 rows
                stripeStatePtr = ref Unsafe.Add(ref stripeStatePtr, _stateStripeStride);
                stripeCoeffPtr = ref Unsafe.Add(ref stripeCoeffPtr, _coeffStripeStride);
            }

            if (passInSequence == 2)
            {
                currentBitPlane--;
            }

            if (_terminateOnPass)
            {
                // TODO: [LOW] Implement proper MQ termination per ITU-T T.800 D.4.1 when terminate-on-pass is signalled.
                // For now, this flag is parsed but termination handling is not yet applied.
            }

            if (_resetContexts)
            {
                _mqDecoder.Reset();
            }

            currentPass++;
        }

        ArrayPool<uint>.Shared.Return(_state);

        return _coefficients;
    }

    /// <summary>
    /// Computes the significance context label (0-8) from the packed neighbor bits
    /// stored in the sample's own state word. Implements ITU-T T.800 Tables D.1–D.3.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly int GetSignificanceContext(uint stateVal, bool verticallyCausal)
    {
        int mask = (int)((stateVal >> 4) & 0xFF);

        if (verticallyCausal)
        {
            mask &= VcNeighborMask;
        }

        return _significanceContextLut[_orientation * 256 + mask];
    }

    /// <summary>
    /// Gets the magnitude refinement context label (0-2) per ITU-T T.800 Table D.4.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetMagnitudeRefinementContext(uint stateVal)
    {
        if ((stateVal & FlagRefined) != 0)
        {
            return 2;
        }

        return (stateVal & NeighborMask) != 0 ? 1 : 0;
    }

    /// <summary>
    /// Marks a sample as significant, sets its sign, writes the magnitude bit
    /// with 1/2 LSB approximation, and propagates neighbor-significance bits
    /// to all 8 adjacent state entries.
    /// </summary>
    /// <param name="coeffRef">Reference already positioned at the target sample's coefficient.</param>
    /// <param name="stateRef">Reference already positioned at the target sample's state entry.</param>
    /// <param name="stateWidth">Row stride of the state array.</param>
    /// <param name="bitPosition">Current bit-plane position.</param>
    /// <param name="sign">Decoded sign (0 = positive, 1 = negative).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetSignificant(
        ref int coeffRef,
        ref uint stateRef,
        int stateWidth,
        int bitPosition,
        int sign)
    {
        uint flags = FlagSignificant;

        if (sign != 0)
        {
            flags |= FlagSign;
        }

        stateRef |= flags;

        int setmask = (int)(((long)3 << bitPosition) >> 1);
        coeffRef = (sign << 31) | setmask;

        // Propagate neighbor-significance bits to all 8 adjacent entries using relative offsets.
        Unsafe.Add(ref stateRef, -1) |= NeighborRight;
        Unsafe.Add(ref stateRef, 1) |= NeighborLeft;
        Unsafe.Add(ref stateRef, -stateWidth) |= NeighborBottom;
        Unsafe.Add(ref stateRef, stateWidth) |= NeighborTop;
        Unsafe.Add(ref stateRef, -stateWidth - 1) |= NeighborBottomRight;
        Unsafe.Add(ref stateRef, -stateWidth + 1) |= NeighborBottomLeft;
        Unsafe.Add(ref stateRef, stateWidth - 1) |= NeighborTopRight;
        Unsafe.Add(ref stateRef, stateWidth + 1) |= NeighborTopLeft;
    }

    /// <summary>
    /// Clears the <see cref="FlagCoded"/> bit from all state entries.
    /// Processes 2 entries at a time using 64-bit operations for improved throughput.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void ClearCodedFlags()
    {
        const ulong clearMask64 = 0xFFFF_FFFB_FFFF_FFFBUL;
        const uint clearMask32 = ~FlagCoded;

        ref uint stateRef = ref _state[0];
        ref ulong longRef = ref Unsafe.As<uint, ulong>(ref stateRef);
        int longCount = _stateLength >> 1;

        for (int j = 0; j < longCount; j++)
        {
            Unsafe.Add(ref longRef, j) &= clearMask64;
        }

        if ((_stateLength & 1) != 0)
        {
            Unsafe.Add(ref stateRef, _stateLength - 1) &= clearMask32;
        }
    }

    /// <summary>
    /// Determines subband orientation from the code-block's resolution and subband index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOrientation(JpxCodeBlock codeBlock)
    {
        if (codeBlock.ResolutionLevel == 0)
        {
            return OrientLL;
        }

        return codeBlock.SubbandIndex switch
        {
            0 => OrientHL,
            1 => OrientLH,
            2 => OrientHH,
            _ => OrientLH
        };
    }

    /// <summary>
    /// ITU-T T.800 Table D.3: HH subband context mapping.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MapHHContext(int hv, int diagonal)
    {
        if (diagonal >= 3)
        {
            return 8;
        }

        if (diagonal == 2)
        {
            return hv >= 1 ? 7 : 6;
        }

        if (diagonal == 1)
        {
            if (hv >= 2)
            {
                return 5;
            }

            return hv == 1 ? 4 : 3;
        }

        if (hv >= 2)
        {
            return 2;
        }

        return hv == 1 ? 1 : 0;
    }

    /// <summary>
    /// ITU-T T.800 Table D.1/D.2: LL/LH/HL subband context mapping.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MapLLLHContext(int primary, int secondary, int diagonal)
    {
        if (primary >= 2)
        {
            return 8;
        }

        if (primary == 1)
        {
            if (secondary >= 1)
            {
                return 7;
            }

            return diagonal >= 1 ? 6 : 5;
        }

        if (secondary >= 2)
        {
            return 4;
        }

        if (secondary == 1)
        {
            return 3;
        }

        if (diagonal >= 2)
        {
            return 2;
        }

        return diagonal == 1 ? 1 : 0;
    }

    /// <summary>
    /// Builds the precomputed significance context lookup table for all orientations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] BuildSignificanceContextLut()
    {
        var lut = new byte[4 * 256];

        for (int orient = 0; orient < 4; orient++)
        {
            for (int mask = 0; mask < 256; mask++)
            {
                int h = (mask & 1) + ((mask >> 1) & 1);
                int v = ((mask >> 2) & 1) + ((mask >> 3) & 1);
                int d = ((mask >> 4) & 1) + ((mask >> 5) & 1) + ((mask >> 6) & 1) + ((mask >> 7) & 1);

                int ctx;

                if (orient == OrientHH)
                {
                    ctx = MapHHContext(h + v, d);
                }
                else
                {
                    int primary;
                    int secondary;

                    if (orient == OrientHL)
                    {
                        primary = v;
                        secondary = h;
                    }
                    else
                    {
                        primary = h;
                        secondary = v;
                    }

                    ctx = MapLLLHContext(primary, secondary, d);
                }

                lut[orient * 256 + mask] = (byte)ctx;
            }
        }

        return lut;
    }
}

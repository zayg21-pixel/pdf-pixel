using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Shared context computation and coding helpers for EBCOT Tier-1 passes.
/// Implements ITU-T T.800 Tables D.1–D.4 for significance, sign, and magnitude contexts.
/// </summary>
internal static class JpxTier1Context
{
    // --- State flag bits (per sample) ---

    /// <summary>Sample has become significant (magnitude > 0).</summary>
    internal const byte FlagSignificant = 0x01;

    /// <summary>Sign bit: 0 = positive, 1 = negative.</summary>
    internal const byte FlagSign = 0x02;

    /// <summary>Sample was coded in the current bit-plane (visited flag).</summary>
    internal const byte FlagCoded = 0x04;

    /// <summary>Sample has been refined at least once (for magnitude refinement context).</summary>
    internal const byte FlagRefined = 0x08;

    // --- Context labels (ITU-T T.800 Table D.1 / D.2) ---

    /// <summary>Context label for the uniform context (context 0).</summary>
    internal const int ContextUniform = 0;

    /// <summary>Context label for the run-length context (context 1).</summary>
    internal const int ContextRunLength = 1;

    /// <summary>Offset added to zero-coding (ZC) context values (0-8) to get context labels 2-10.</summary>
    internal const int ContextZcOffset = 2;

    /// <summary>Offset added to sign-coding (SC) context values (0-4) to get context labels 11-15.</summary>
    internal const int ContextScOffset = 11;

    /// <summary>Offset added to magnitude-refinement (MR) context values (0-2) to get context labels 16-18.</summary>
    internal const int ContextMrOffset = 16;

    /// <summary>
    /// Bit position of the most significant magnitude bit in the sign-magnitude coefficient format.
    /// Bit 31 is the sign bit; bit 30 is the MSB of magnitude.
    /// </summary>
    internal const int MagnitudeBitPosition = 30;

    // Subband orientation constants (matches CoreJ2K Subband.WT_ORIENT_*)
    internal const int OrientLL = 0;
    internal const int OrientHL = 1;
    internal const int OrientLH = 2;
    internal const int OrientHH = 3;

    /// <summary>
    /// Computes the significance context label (0-8) from the 8-connected neighbours.
    /// Implements ITU-T T.800 Table D.1 (LL/LH), Table D.2 (HL), and Table D.3 (HH).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetSignificanceContext(byte[] state, int stateIndex, int stateWidth, bool verticallyCausal, int orientation)
    {
        int horizontal = 0;
        int vertical = 0;
        int diagonal = 0;

        if ((state[stateIndex - 1] & FlagSignificant) != 0)
        {
            horizontal++;
        }

        if ((state[stateIndex + 1] & FlagSignificant) != 0)
        {
            horizontal++;
        }

        if ((state[stateIndex - stateWidth] & FlagSignificant) != 0)
        {
            vertical++;
        }

        if (!verticallyCausal)
        {
            if ((state[stateIndex + stateWidth] & FlagSignificant) != 0)
            {
                vertical++;
            }

            if ((state[stateIndex + stateWidth - 1] & FlagSignificant) != 0)
            {
                diagonal++;
            }

            if ((state[stateIndex + stateWidth + 1] & FlagSignificant) != 0)
            {
                diagonal++;
            }
        }

        if ((state[stateIndex - stateWidth - 1] & FlagSignificant) != 0)
        {
            diagonal++;
        }

        if ((state[stateIndex - stateWidth + 1] & FlagSignificant) != 0)
        {
            diagonal++;
        }

        if (orientation == OrientHH)
        {
            return MapHHContext(horizontal + vertical, diagonal);
        }

        int primary;
        int secondary;

        if (orientation == OrientHL)
        {
            primary = vertical;
            secondary = horizontal;
        }
        else
        {
            primary = horizontal;
            secondary = vertical;
        }

        return MapLLLHContext(primary, secondary, diagonal);
    }

    /// <summary>
    /// Gets the magnitude refinement context label (0-2) per ITU-T T.800 Table D.4.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetMagnitudeRefinementContext(byte[] state, int stateIndex, int stateWidth)
    {
        if ((state[stateIndex] & FlagRefined) != 0)
        {
            return 2;
        }

        bool hasSignificantNeighbour =
            (state[stateIndex - 1] & FlagSignificant) != 0 ||
            (state[stateIndex + 1] & FlagSignificant) != 0 ||
            (state[stateIndex - stateWidth] & FlagSignificant) != 0 ||
            (state[stateIndex + stateWidth] & FlagSignificant) != 0 ||
            (state[stateIndex - stateWidth - 1] & FlagSignificant) != 0 ||
            (state[stateIndex - stateWidth + 1] & FlagSignificant) != 0 ||
            (state[stateIndex + stateWidth - 1] & FlagSignificant) != 0 ||
            (state[stateIndex + stateWidth + 1] & FlagSignificant) != 0;

        return hasSignificantNeighbour ? 1 : 0;
    }

    /// <summary>
    /// Decodes the sign of a newly significant sample per ITU-T T.800 Table D.3.
    /// </summary>
    /// <returns>0 for positive, 1 for negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int DecodeSign(ref JpxMqDecoder mqDecoder, byte[] state, int stateIndex, int stateWidth)
    {
        int horizontalContribution = GetSignContribution(state, stateIndex - 1, stateIndex + 1);
        int verticalContribution = GetSignContribution(state, stateIndex - stateWidth, stateIndex + stateWidth);

        int combined = horizontalContribution * 3 + verticalContribution;

        int contextIndex;
        int xorBit;

        switch (combined)
        {
            case 4:
                contextIndex = ContextScOffset + 4;
                xorBit = 0;
                break;
            case 3:
                contextIndex = ContextScOffset + 3;
                xorBit = 0;
                break;
            case 2:
                contextIndex = ContextScOffset + 2;
                xorBit = 0;
                break;
            case 1:
                contextIndex = ContextScOffset + 1;
                xorBit = 0;
                break;
            case 0:
                contextIndex = ContextScOffset;
                xorBit = 0;
                break;
            case -1:
                contextIndex = ContextScOffset + 1;
                xorBit = 1;
                break;
            case -2:
                contextIndex = ContextScOffset + 2;
                xorBit = 1;
                break;
            case -3:
                contextIndex = ContextScOffset + 3;
                xorBit = 1;
                break;
            case -4:
                contextIndex = ContextScOffset + 4;
                xorBit = 1;
                break;
            default:
                contextIndex = ContextScOffset;
                xorBit = 0;
                break;
        }

        int decodedBit = mqDecoder.DecodeBit(contextIndex);
        return decodedBit ^ xorBit;
    }

    /// <summary>
    /// Marks a sample as significant, sets its sign, and writes the magnitude bit
    /// with 1/2 LSB approximation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetSignificant(
        int[] coefficients,
        byte[] state,
        int stateIndex,
        int x,
        int y,
        int width,
        int bitPosition,
        int sign)
    {
        state[stateIndex] |= FlagSignificant;

        if (sign != 0)
        {
            state[stateIndex] |= FlagSign;
        }

        int setmask = (int)(((long)3 << bitPosition) >> 1);
        coefficients[y * width + x] = (sign << 31) | setmask;
    }

    /// <summary>
    /// Clears the <see cref="FlagCoded"/> bit from all state entries.
    /// </summary>
    internal static void ClearCodedFlags(byte[] state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            state[i] &= unchecked((byte)~FlagCoded);
        }
    }

    /// <summary>
    /// Gets the sign contribution (-1, 0, or +1) from a pair of opposing neighbours.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSignContribution(byte[] state, int minusIndex, int plusIndex)
    {
        bool minusSignificant = (state[minusIndex] & FlagSignificant) != 0;
        bool plusSignificant = (state[plusIndex] & FlagSignificant) != 0;

        if (!minusSignificant && !plusSignificant)
        {
            return 0;
        }

        int contribution = 0;

        if (minusSignificant)
        {
            contribution += (state[minusIndex] & FlagSign) != 0 ? -1 : 1;
        }

        if (plusSignificant)
        {
            contribution += (state[plusIndex] & FlagSign) != 0 ? -1 : 1;
        }

        if (contribution > 0)
        {
            return 1;
        }

        if (contribution < 0)
        {
            return -1;
        }

        return 0;
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
}

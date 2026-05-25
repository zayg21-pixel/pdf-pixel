using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

#pragma warning disable RCS1019 // Order modifiers
internal ref partial struct JpxTier1Decoder
#pragma warning restore RCS1019 // Order modifiers
{
    /// <summary>
    /// Decodes the sign of a newly significant sample per ITU-T T.800 Table D.3.
    /// Uses precomputed lookup tables to eliminate the 9-case switch.
    /// </summary>
    /// <param name="statePtr">Reference positioned at the current sample's state entry.</param>
    /// <returns>0 for positive, 1 for negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DecodeSign(ref uint statePtr)
    {
        int stateWidth = _stateWidth;
        uint leftState = Unsafe.Add(ref statePtr, -1);
        uint rightState = Unsafe.Add(ref statePtr, 1);
        uint topState = Unsafe.Add(ref statePtr, -stateWidth);
        uint bottomState = Unsafe.Add(ref statePtr, stateWidth);

        int horizontalContribution = ComputeContribution(leftState, rightState);
        int verticalContribution = ComputeContribution(topState, bottomState);

        int combined = (horizontalContribution * 3) + verticalContribution + 4;

        int contextIndex = ContextScOffset + _signContextLabel[combined];
        int xorBit = _signContextXorBit[combined];

        int decodedBit = _mqDecoder.DecodeBit(contextIndex);
        return decodedBit ^ xorBit;
    }

    /// <summary>
    /// Computes the sign contribution (-1, 0, or +1) from a pair of opposing neighbours.
    /// Branchless: extracts significance and sign from each neighbor's state word,
    /// computes individual signed contributions, sums, and clamps to {-1, 0, 1}.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeContribution(uint minusState, uint plusState)
    {
        // Extract significance (bit 0) and sign (bit 1) from each neighbor
        var minusSig = (int)(minusState & FlagSignificant);
        var minusSign = (int)((minusState & FlagSign) >> 1);
        // contribution = sig * (1 - 2*sign) = sig - 2*sig*sign
        int minusContrib = minusSig - (2 * (minusSig & minusSign));

        var plusSig = (int)(plusState & FlagSignificant);
        var plusSign = (int)((plusState & FlagSign) >> 1);
        int plusContrib = plusSig - (2 * (plusSig & plusSign));

        int total = minusContrib + plusContrib;

        // Branchless clamp to {-1, 0, 1}: sign(total)
        return (total >> 31) | (int)((uint)(-total) >> 31);
    }
}

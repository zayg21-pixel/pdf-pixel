using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

#pragma warning disable RCS1019 // Order modifiers
internal ref partial struct JpxTier1Decoder
#pragma warning restore RCS1019 // Order modifiers
{
    /// <summary>
    /// Magnitude Refinement Pass (ITU-T T.800 D.3.2) for a single stripe.
    /// Refines samples that are already significant by adding one more bit of precision.
    /// </summary>
    /// <param name="stripeStatePtr">Reference to the first state entry of the stripe (row 0, col 0).</param>
    /// <param name="stripeCoeffPtr">Reference to the first coefficient of the stripe (row 0, col 0).</param>
    /// <param name="stripeHeight">Number of rows in this stripe (1–4).</param>
    /// <param name="bitPosition">Current bit-plane position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteMagnitudeRefinement(
        ref uint stripeStatePtr,
        ref int stripeCoeffPtr,
        int stripeHeight,
        int bitPosition)
    {
        int stateWidth = _stateWidth;
        int width = _width;
        int resetmask = (-1) << (bitPosition + 1);
        int setmask = (1 << bitPosition) >> 1;

        for (int x = 0; x < width; x++)
        {
            ref uint statePtr = ref Unsafe.Add(ref stripeStatePtr, x);
            ref int coeffPtr = ref Unsafe.Add(ref stripeCoeffPtr, x);

            for (int row = 0; row < stripeHeight; row++)
            {
                uint stateVal = statePtr;

                // Only refine samples that are significant and not coded this pass
                if ((stateVal & (FlagSignificant | FlagCoded)) == FlagSignificant)
                {
                    int magContext = GetMagnitudeRefinementContext(stateVal);
                    int bit = _mqDecoder.DecodeBit(magContext + ContextMrOffset);

                    // Update coefficient: clear old 1/2 approx, set decoded bit + new 1/2 approx.
                    // The reset mask spans every bit above this plane, so it carries the sign
                    // through untouched and the decoded bits never reach it.
                    coeffPtr = (coeffPtr & resetmask) | (bit << bitPosition) | setmask;

                    // Mark as coded and refined
                    statePtr |= FlagCoded | FlagRefined;
                }

                statePtr = ref Unsafe.Add(ref statePtr, stateWidth);
                coeffPtr = ref Unsafe.Add(ref coeffPtr, width);
            }
        }
    }
}

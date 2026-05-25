using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

#pragma warning disable RCS1019 // Order modifiers
internal ref partial struct JpxTier1Decoder
#pragma warning restore RCS1019 // Order modifiers
{
    /// <summary>
    /// Significance Propagation Pass (ITU-T T.800 D.3.1) for a single stripe.
    /// Visits insignificant samples that have at least one significant neighbour.
    /// </summary>
    /// <param name="stripeStatePtr">Reference to the first state entry of the stripe (row 0, col 0).</param>
    /// <param name="stripeCoeffPtr">Reference to the first coefficient of the stripe (row 0, col 0).</param>
    /// <param name="stripeHeight">Number of rows in this stripe (1–4).</param>
    /// <param name="bitPosition">Current bit-plane position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteSignificancePropagation(
        ref uint stripeStatePtr,
        ref int stripeCoeffPtr,
        int stripeHeight,
        int bitPosition)
    {
        int stateWidth = _stateWidth;
        int width = _width;
        bool verticallyCausal = _verticallyCausal;

        for (int x = 0; x < width; x++)
        {
            ref uint statePtr = ref Unsafe.Add(ref stripeStatePtr, x);
            ref int coeffPtr = ref Unsafe.Add(ref stripeCoeffPtr, x);

            for (int row = 0; row < stripeHeight; row++)
            {
                uint stateVal = statePtr;

                if ((stateVal & FlagSignificantOrCoded) == 0)
                {
                    bool vcRow = verticallyCausal && row == 0;
                    int sigContext = GetSignificanceContext(stateVal, vcRow);

                    if (sigContext != 0)
                    {
                        int significant = _mqDecoder.DecodeBit(sigContext + ContextZcOffset);
                        statePtr |= FlagCoded;

                        if (significant != 0)
                        {
                            int sign = DecodeSign(ref statePtr);
                            SetSignificant(ref coeffPtr, ref statePtr, stateWidth, bitPosition, sign);
                        }
                    }
                }

                statePtr = ref Unsafe.Add(ref statePtr, stateWidth);
                coeffPtr = ref Unsafe.Add(ref coeffPtr, width);
            }
        }
    }
}

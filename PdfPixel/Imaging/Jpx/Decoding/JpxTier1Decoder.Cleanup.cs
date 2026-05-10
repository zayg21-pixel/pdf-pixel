using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jpx.Decoding;

internal ref partial struct JpxTier1Decoder
{
    /// <summary>
    /// Cleanup Pass (ITU-T T.800 D.3.3) for a single stripe.
    /// Codes all remaining insignificant samples not visited by the significance propagation pass.
    /// Uses run-length coding when a full column of 4 samples has no significant neighbours.
    /// </summary>
    /// <param name="stripeStatePtr">Reference to the first state entry of the stripe (row 0, col 0).</param>
    /// <param name="stripeCoeffPtr">Reference to the first coefficient of the stripe (row 0, col 0).</param>
    /// <param name="stripeTop">Absolute row index of the stripe top (needed for vertically-causal context).</param>
    /// <param name="stripeHeight">Number of rows in this stripe (1–4).</param>
    /// <param name="bitPosition">Current bit-plane position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteCleanup(
        ref uint stripeStatePtr,
        ref int stripeCoeffPtr,
        int stripeTop,
        int stripeHeight,
        int bitPosition)
    {
        int stateWidth = _stateWidth;
        int width = _width;
        bool verticallyCausal = _verticallyCausal;
        bool isFullStripe = stripeHeight == 4;

        for (int x = 0; x < width; x++)
        {
            ref uint colStatePtr = ref Unsafe.Add(ref stripeStatePtr, x);
            bool canRunLength = isFullStripe && CanUseRunLength(ref colStatePtr, stateWidth);

            int rowsToSkip = 0;

            if (canRunLength)
            {
                // Decode run-length bit: 0 = all insignificant, 1 = has significant sample
                int runBit = _mqDecoder.DecodeBit(ContextRunLength);

                if (runBit == 0)
                {
                    // All 4 samples are zero — skip entire stripe column
                    continue;
                }

                // Decode which sample in the stripe is the first significant one
                int position = _mqDecoder.DecodeBit(ContextUniform) << 1;
                position |= _mqDecoder.DecodeBit(ContextUniform);

                // Mark samples before the significant one as coded (zero)
                ref uint markPtr = ref colStatePtr;
                for (int k = 0; k < position; k++)
                {
                    markPtr |= FlagCoded;
                    markPtr = ref Unsafe.Add(ref markPtr, stateWidth);
                }

                // The sample at 'position' is significant
                ref uint sigStatePtr = ref Unsafe.Add(ref colStatePtr, position * stateWidth);
                ref int sigCoeffPtr = ref Unsafe.Add(ref stripeCoeffPtr, position * width + x);
                int sign = DecodeSign(ref sigStatePtr);
                SetSignificant(ref sigCoeffPtr, ref sigStatePtr, stateWidth, bitPosition, sign);
                sigStatePtr |= FlagCoded;

                rowsToSkip = position + 1;
            }

            // Process remaining samples in the stripe
            ref uint statePtr = ref Unsafe.Add(ref colStatePtr, rowsToSkip * stateWidth);
            ref int coeffPtr = ref Unsafe.Add(ref stripeCoeffPtr, rowsToSkip * width + x);

            for (int row = rowsToSkip; row < stripeHeight; row++)
            {
                uint stateVal = statePtr;

                if ((stateVal & FlagSignificantOrCoded) == 0)
                {
                    int sigContext = GetSignificanceContext(stateVal, verticallyCausal && row == 0);
                    int significant = _mqDecoder.DecodeBit(sigContext + ContextZcOffset);

                    if (significant != 0)
                    {
                        int sign = DecodeSign(ref statePtr);
                        SetSignificant(ref coeffPtr, ref statePtr, stateWidth, bitPosition, sign);
                    }

                    statePtr |= FlagCoded;
                }

                statePtr = ref Unsafe.Add(ref statePtr, stateWidth);
                coeffPtr = ref Unsafe.Add(ref coeffPtr, width);
            }
        }

        // Verify segmentation symbol if enabled (ITU-T T.800 D.4.1)
        if (_segmentationSymbols)
        {
            int symbol = 0;
            symbol = (symbol << 1) | _mqDecoder.DecodeBit(ContextUniform);
            symbol = (symbol << 1) | _mqDecoder.DecodeBit(ContextUniform);
            symbol = (symbol << 1) | _mqDecoder.DecodeBit(ContextUniform);
            symbol = (symbol << 1) | _mqDecoder.DecodeBit(ContextUniform);

            if (symbol != 0x0A)
            {
                throw new InvalidOperationException(
                    $"Segmentation symbol mismatch: expected 0x0A, got 0x{symbol:X2}. Data may be corrupt.");
            }
        }
    }

    /// <summary>
    /// Checks if run-length coding can be used for a 4-row stripe column.
    /// All 4 samples must be insignificant, uncoded, and have no significant neighbours.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanUseRunLength(ref uint colStatePtr, int stateWidth)
    {
        uint combined = colStatePtr;
        combined |= Unsafe.Add(ref colStatePtr, stateWidth);
        combined |= Unsafe.Add(ref colStatePtr, stateWidth * 2);
        combined |= Unsafe.Add(ref colStatePtr, stateWidth * 3);

        return (combined & RunLengthCheckMask) == 0;
    }
}

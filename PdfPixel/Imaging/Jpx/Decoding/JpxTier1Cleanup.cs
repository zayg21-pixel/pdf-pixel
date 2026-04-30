using System;
using static PdfPixel.Imaging.Jpx.Decoding.JpxTier1Context;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Cleanup Pass (ITU-T T.800 D.3.3).
/// Codes all remaining insignificant samples not visited by the significance propagation pass.
/// Uses run-length coding when a full column of 4 samples has no significant neighbours.
/// </summary>
internal static class JpxTier1Cleanup
{
    /// <summary>
    /// Executes the cleanup pass over the code-block.
    /// </summary>
    internal static void Execute(
        ref JpxMqDecoder mqDecoder,
        int[] coefficients,
        byte[] state,
        int width,
        int height,
        int stateWidth,
        int bitPosition,
        bool verticallyCausal,
        bool segmentationSymbols,
        int orientation)
    {
        for (int stripeTop = 0; stripeTop < height; stripeTop += 4)
        {
            int stripeBottom = Math.Min(stripeTop + 4, height);
            int stripeHeight = stripeBottom - stripeTop;

            for (int x = 0; x < width; x++)
            {
                bool canRunLength = stripeHeight == 4 && CanUseRunLength(state, stateWidth, x, stripeTop, orientation);

                int runStartY = stripeTop;

                if (canRunLength)
                {
                    // Decode run-length bit: 0 = all insignificant, 1 = has significant sample
                    int runBit = mqDecoder.DecodeBit(ContextRunLength);

                    if (runBit == 0)
                    {
                        // All 4 samples are zero — skip entire stripe column
                        continue;
                    }

                    // Decode which sample in the stripe is the first significant one
                    int position = mqDecoder.DecodeBit(ContextUniform) << 1;
                    position |= mqDecoder.DecodeBit(ContextUniform);

                    // Mark samples before the significant one as coded (zero)
                    for (int k = 0; k < position; k++)
                    {
                        int si = (stripeTop + k + 1) * stateWidth + (x + 1);
                        state[si] |= FlagCoded;
                    }

                    // The sample at 'position' is significant
                    int sigY = stripeTop + position;
                    int sigStateIndex = (sigY + 1) * stateWidth + (x + 1);
                    int sign = DecodeSign(ref mqDecoder, state, sigStateIndex, stateWidth);
                    SetSignificant(coefficients, state, sigStateIndex, x, sigY, width, bitPosition, sign);
                    state[sigStateIndex] |= FlagCoded;

                    runStartY = sigY + 1;
                }

                // Process remaining samples in the stripe
                for (int y = runStartY; y < stripeBottom; y++)
                {
                    int stateIndex = (y + 1) * stateWidth + (x + 1);

                    if ((state[stateIndex] & (FlagSignificant | FlagCoded)) != 0)
                    {
                        continue;
                    }

                    int sigContext = GetSignificanceContext(state, stateIndex, stateWidth, verticallyCausal && y == stripeTop, orientation);

                    int significant = mqDecoder.DecodeBit(sigContext + ContextZcOffset);

                    if (significant != 0)
                    {
                        int sign = DecodeSign(ref mqDecoder, state, stateIndex, stateWidth);
                        SetSignificant(coefficients, state, stateIndex, x, y, width, bitPosition, sign);
                    }

                    state[stateIndex] |= FlagCoded;
                }
            }
        }

        // Verify segmentation symbol if enabled (ITU-T T.800 D.4.1)
        if (segmentationSymbols)
        {
            int symbol = 0;
            symbol = (symbol << 1) | mqDecoder.DecodeBit(ContextUniform);
            symbol = (symbol << 1) | mqDecoder.DecodeBit(ContextUniform);
            symbol = (symbol << 1) | mqDecoder.DecodeBit(ContextUniform);
            symbol = (symbol << 1) | mqDecoder.DecodeBit(ContextUniform);

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
    private static bool CanUseRunLength(byte[] state, int stateWidth, int x, int stripeTop, int orientation)
    {
        for (int k = 0; k < 4; k++)
        {
            int si = (stripeTop + k + 1) * stateWidth + (x + 1);

            if ((state[si] & (FlagSignificant | FlagCoded)) != 0)
            {
                return false;
            }

            if (GetSignificanceContext(state, si, stateWidth, false, orientation) != 0)
            {
                return false;
            }
        }

        return true;
    }
}

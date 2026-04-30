using System;
using static PdfPixel.Imaging.Jpx.Decoding.JpxTier1Context;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Significance Propagation Pass (ITU-T T.800 D.3.1).
/// Visits insignificant samples that have at least one significant neighbour.
/// Scans in stripe-column order (4-row stripes).
/// </summary>
internal static class JpxTier1SignificancePropagation
{
    /// <summary>
    /// Executes the significance propagation pass over the code-block.
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
        int orientation)
    {
        for (int stripeTop = 0; stripeTop < height; stripeTop += 4)
        {
            int stripeBottom = Math.Min(stripeTop + 4, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = stripeTop; y < stripeBottom; y++)
                {
                    int stateIndex = (y + 1) * stateWidth + (x + 1);

                    // Skip if already significant or already coded this pass
                    if ((state[stateIndex] & (FlagSignificant | FlagCoded)) != 0)
                    {
                        continue;
                    }

                    // Check if any neighbour is significant
                    int sigContext = GetSignificanceContext(state, stateIndex, stateWidth, verticallyCausal && y == stripeTop, orientation);

                    if (sigContext == 0)
                    {
                        // No significant neighbours — will be handled in cleanup
                        continue;
                    }

                    // Decode significance bit (ZC contexts are 2-10)
                    int significant = mqDecoder.DecodeBit(sigContext + ContextZcOffset);
                    state[stateIndex] |= FlagCoded;

                    if (significant != 0)
                    {
                        int sign = DecodeSign(ref mqDecoder, state, stateIndex, stateWidth);
                        SetSignificant(coefficients, state, stateIndex, x, y, width, bitPosition, sign);
                    }
                }
            }
        }
    }
}

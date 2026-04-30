using System;
using static PdfPixel.Imaging.Jpx.Decoding.JpxTier1Context;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Magnitude Refinement Pass (ITU-T T.800 D.3.2).
/// Refines samples that are already significant by adding one more bit of precision.
/// </summary>
internal static class JpxTier1MagnitudeRefinement
{
    /// <summary>
    /// Executes the magnitude refinement pass over the code-block.
    /// </summary>
    internal static void Execute(
        ref JpxMqDecoder mqDecoder,
        int[] coefficients,
        byte[] state,
        int width,
        int height,
        int stateWidth,
        int bitPosition)
    {
        for (int stripeTop = 0; stripeTop < height; stripeTop += 4)
        {
            int stripeBottom = Math.Min(stripeTop + 4, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = stripeTop; y < stripeBottom; y++)
                {
                    int stateIndex = (y + 1) * stateWidth + (x + 1);

                    // Only refine samples that are already significant and not coded this pass
                    if ((state[stateIndex] & FlagSignificant) == 0)
                    {
                        continue;
                    }

                    if ((state[stateIndex] & FlagCoded) != 0)
                    {
                        continue;
                    }

                    // Get magnitude refinement context (MR contexts are 16-18)
                    int magContext = GetMagnitudeRefinementContext(state, stateIndex, stateWidth);

                    int bit = mqDecoder.DecodeBit(magContext + ContextMrOffset);
                    state[stateIndex] |= FlagCoded;

                    // Update coefficient: clear old 1/2 approx, set decoded bit + new 1/2 approx
                    int resetmask = (-1) << (bitPosition + 1);
                    int setmask = (1 << bitPosition) >> 1;
                    int signBit = coefficients[y * width + x] & unchecked((int)0x80000000);
                    int magnitude = coefficients[y * width + x] & 0x7FFFFFFF;
                    magnitude &= resetmask;
                    magnitude |= (bit << bitPosition) | setmask;
                    coefficients[y * width + x] = signBit | magnitude;

                    // Mark as refined
                    state[stateIndex] |= FlagRefined;
                }
            }
        }
    }
}

using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Places decoded code-block coefficients into their position within a component's
/// subband arrays. This is Stage 3 of the JPEG 2000 decoding pipeline per ITU-T T.800.
/// </summary>
internal static class JpxSubbandAssembler
{
    /// <summary>
    /// Copies a decoded code-block's coefficients into the subband it belongs to.
    /// </summary>
    /// <param name="codeBlock">The code-block whose position within the subband is used.</param>
    /// <param name="coefficients">The code-block's decoded coefficients, row-major.</param>
    /// <param name="subbands">Target subband data for the code-block's component.</param>
    public static void Place(JpxCodeBlock codeBlock, in ReadOnlySpan<int> coefficients, JpxSubbandData subbands)
    {
        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }

        if (subbands == null)
        {
            throw new ArgumentNullException(nameof(subbands));
        }

        if (codeBlock.ResolutionLevel == 0)
        {
            // Resolution 0 = LL subband (lowest frequency)
            PlaceCodeBlock(subbands.LL, subbands.LLWidth, subbands.LLHeight, codeBlock, coefficients);
            return;
        }

        // Resolution r > 0 has three subbands: HL, LH, HH.
        // Resolution 1 (coarsest detail, smallest) → level Levels-1 in JpxSubbandData
        // Resolution N (finest detail, largest) → level 0 in JpxSubbandData
        int subbandLevel = subbands.Levels - codeBlock.ResolutionLevel;
        var subbandType = (JpxSubbandType)codeBlock.SubbandIndex;

        PlaceCodeBlock(
            subbands.GetSubband(subbandLevel, subbandType),
            subbands.GetWidth(subbandLevel, subbandType),
            subbands.GetHeight(subbandLevel, subbandType),
            codeBlock,
            coefficients);
    }

    /// <summary>
    /// Places a single code-block's coefficients into the target subband array.
    /// </summary>
    private static void PlaceCodeBlock(
        in Span<int> subbandData,
        int subbandWidth,
        int subbandHeight,
        JpxCodeBlock codeBlock,
        in ReadOnlySpan<int> coefficients)
    {
        int cbStartX = codeBlock.SubbandX;
        int cbStartY = codeBlock.SubbandY;
        int coeffWidth = Math.Min(codeBlock.Width, subbandWidth - cbStartX);
        int coeffHeight = Math.Min(codeBlock.Height, subbandHeight - cbStartY);

        if (coeffWidth <= 0 || coeffHeight <= 0)
        {
            return;
        }

        for (int y = 0; y < coeffHeight; y++)
        {
            int destY = cbStartY + y;
            if (destY >= subbandHeight)
            {
                break;
            }

            coefficients.Slice(y * codeBlock.Width, coeffWidth)
                .CopyTo(subbandData.Slice((destY * subbandWidth) + cbStartX, coeffWidth));
        }
    }
}

using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// EBCOT Tier-1 code-block decoder per ITU-T T.800 Annex D.
/// Decodes entropy-coded code-block data into wavelet coefficients using three coding pass types:
/// significance propagation, magnitude refinement, and cleanup.
/// </summary>
/// <remarks>
/// Each code-block is decoded independently. The decoder maintains per-sample state flags
/// that track significance, sign, and refinement status. Coding passes are processed in order
/// (cleanup first at MSB, then significance/magnitude/cleanup cycling) building up the
/// coefficient magnitudes bit-plane by bit-plane.
/// </remarks>
internal sealed class JpxTier1Decoder
{
    private readonly bool _resetContexts;
    private readonly bool _terminateOnPass;
    private readonly bool _verticallyCausal;
    private readonly bool _segmentationSymbols;

    /// <summary>
    /// Creates a Tier-1 decoder configured with the given coding style.
    /// </summary>
    /// <param name="codingStyle">Coding style parameters from the codestream header.</param>
    public JpxTier1Decoder(JpxCodingStyle codingStyle)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        _resetContexts = (codingStyle.CodeBlockStyle & 0x02) != 0;
        _terminateOnPass = (codingStyle.CodeBlockStyle & 0x04) != 0;
        _verticallyCausal = (codingStyle.CodeBlockStyle & 0x08) != 0;
        _segmentationSymbols = (codingStyle.CodeBlockStyle & 0x20) != 0;
    }

    /// <summary>
    /// Decodes a single code-block's entropy-coded data into wavelet coefficients.
    /// </summary>
    /// <param name="codeBlock">The code-block containing compressed data and metadata.</param>
    /// <returns>
    /// A flat array of wavelet coefficients [height * width] in row-major sign-magnitude format at bit 30.
    /// </returns>
    public int[] Decode(JpxCodeBlock codeBlock)
    {
        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }

        int width = codeBlock.Width;
        int height = codeBlock.Height;

        if (width <= 0 || height <= 0)
        {
            return Array.Empty<int>();
        }

        var coefficients = new int[height * width];

        if (codeBlock.Data.Length == 0 || codeBlock.CodingPasses == 0)
        {
            return coefficients;
        }

        // State flags with 1-pixel border for neighbour lookups
        int stateWidth = width + 2;
        int stateHeight = height + 2;
        var state = new byte[stateHeight * stateWidth];

        int orientation = GetOrientation(codeBlock);

        var mqDecoder = new JpxMqDecoder(codeBlock.Data.Span);

        int totalPasses = codeBlock.CodingPasses;
        int currentBitPlane = JpxTier1Context.MagnitudeBitPosition - codeBlock.ZeroBitPlanes;
        int currentPass = 0;

        while (currentPass < totalPasses)
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
                JpxTier1Context.ClearCodedFlags(state);
            }

            switch (passInSequence)
            {
                case 0:
                    JpxTier1SignificancePropagation.Execute(
                        ref mqDecoder, coefficients, state,
                        width, height, stateWidth,
                        currentBitPlane, _verticallyCausal, orientation);
                    break;

                case 1:
                    JpxTier1MagnitudeRefinement.Execute(
                        ref mqDecoder, coefficients, state,
                        width, height, stateWidth,
                        currentBitPlane);
                    break;

                case 2:
                    JpxTier1Cleanup.Execute(
                        ref mqDecoder, coefficients, state,
                        width, height, stateWidth,
                        currentBitPlane, _verticallyCausal, _segmentationSymbols, orientation);
                    break;
            }

            if (passInSequence == 2)
            {
                currentBitPlane--;
            }

            if (_terminateOnPass)
            {
                // TODO: Implement proper MQ termination per ITU-T T.800 D.4.1 when terminate-on-pass is signalled.
                // For now, this flag is parsed but termination handling is not yet applied.
            }

            if (_resetContexts)
            {
                mqDecoder.Reset();
            }

            currentPass++;
        }

        return coefficients;
    }

    /// <summary>
    /// Static convenience method for backwards compatibility with existing callers.
    /// Creates a decoder instance and decodes the code-block.
    /// </summary>
    public static int[] DecodeCodeBlock(JpxCodeBlock codeBlock, JpxCodingStyle codingStyle)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        var decoder = new JpxTier1Decoder(codingStyle);
        return decoder.Decode(codeBlock);
    }

    /// <summary>
    /// Determines subband orientation from the code-block's resolution and subband index.
    /// </summary>
    private static int GetOrientation(JpxCodeBlock codeBlock)
    {
        if (codeBlock.ResolutionLevel == 0)
        {
            return JpxTier1Context.OrientLL;
        }

        return codeBlock.SubbandIndex switch
        {
            0 => JpxTier1Context.OrientHL,
            1 => JpxTier1Context.OrientLH,
            2 => JpxTier1Context.OrientHH,
            _ => JpxTier1Context.OrientLH
        };
    }
}

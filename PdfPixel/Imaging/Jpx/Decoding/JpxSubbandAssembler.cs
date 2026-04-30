using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Assembles decoded code-block coefficients from packets into per-component subband arrays.
/// This is Stage 3 of the JPEG 2000 decoding pipeline per ITU-T T.800.
/// </summary>
internal sealed class JpxSubbandAssembler
{
    private readonly int _codeBlockWidth;
    private readonly int _codeBlockHeight;

    /// <summary>
    /// Creates a subband assembler configured for the given code-block dimensions.
    /// </summary>
    /// <param name="codingStyle">Coding style parameters containing code-block dimensions.</param>
    public JpxSubbandAssembler(JpxCodingStyle codingStyle)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        _codeBlockWidth = codingStyle.CodeBlockWidth;
        _codeBlockHeight = codingStyle.CodeBlockHeight;
    }

    /// <summary>
    /// Assembles code-block coefficients from packets into a subband structure for one component.
    /// </summary>
    /// <param name="packets">Parsed and entropy-decoded packets.</param>
    /// <param name="componentIndex">Which component to assemble.</param>
    /// <param name="subbands">Target subband data to fill with coefficients.</param>
    public void Assemble(JpxPacket[] packets, int componentIndex, JpxSubbandData subbands)
    {
        if (packets == null)
        {
            return;
        }

        if (subbands == null)
        {
            throw new ArgumentNullException(nameof(subbands));
        }

        foreach (var packet in packets)
        {
            if (packet.Component != componentIndex)
            {
                continue;
            }

            if (packet.CodeBlocks == null)
            {
                continue;
            }

            foreach (var codeBlock in packet.CodeBlocks)
            {
                if (codeBlock.DecodedCoefficients == null)
                {
                    continue;
                }

                int resolution = packet.Resolution;

                if (resolution == 0)
                {
                    // Resolution 0 = LL subband (lowest frequency)
                    PlaceCodeBlock(subbands.LL, subbands.LLWidth, subbands.LLHeight, codeBlock);
                }
                else
                {
                    // Resolution r > 0 has three subbands: HL, LH, HH
                    // SubbandIndex: 0 = HL, 1 = LH, 2 = HH
                    // Resolution 1 (coarsest detail, smallest) → level Levels-1 in JpxSubbandData
                    // Resolution N (finest detail, largest) → level 0 in JpxSubbandData
                    int subbandLevel = subbands.Levels - resolution;
                    var subbandType = (JpxSubbandType)codeBlock.SubbandIndex;

                    PlaceCodeBlock(
                        subbands.GetSubband(subbandLevel, subbandType),
                        subbands.GetWidth(subbandLevel, subbandType),
                        subbands.GetHeight(subbandLevel, subbandType),
                        codeBlock);
                }
            }
        }
    }

    /// <summary>
    /// Places a single code-block's coefficients into the target subband array.
    /// </summary>
    private void PlaceCodeBlock(
        int[] subbandData,
        int subbandWidth,
        int subbandHeight,
        JpxCodeBlock codeBlock)
    {
        int cbStartX = codeBlock.X * _codeBlockWidth;
        int cbStartY = codeBlock.Y * _codeBlockHeight;
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

            for (int x = 0; x < coeffWidth; x++)
            {
                int destX = cbStartX + x;
                if (destX >= subbandWidth)
                {
                    break;
                }

                subbandData[destY * subbandWidth + destX] = codeBlock.DecodedCoefficients[y * codeBlock.Width + x];
            }
        }
    }
}

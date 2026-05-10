using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// General JPEG2000 tile decoder implementing the complete decoding pipeline.
/// Handles the standard JPEG2000 decoding stages:
/// 1. Packet Parsing (progression order → packets → code-blocks)
/// 2. Entropy Decoding (MQ arithmetic decoder)
/// 3. Coefficient Assembly (code-blocks → subbands)
/// 4. Inverse Quantization (integrated into DWT for both 5-3 and 9-7)
/// 5. Inverse Wavelet Transform (5-3 reversible or 9-7 irreversible)
/// 6. Inverse MCT, Level Shifting, and Clamping
/// </summary>
internal sealed class JpxTileDecoder : IJpxTileDecoder
{
    private readonly JpxHeader _header;
    private readonly IJpxPacketParser _packetParser;
    private readonly JpxSubbandAssembler _assembler;
    private readonly JpxInverseMct _inverseMct;

    public JpxTileDecoder(JpxHeader header, IJpxPacketParser packetParser)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _packetParser = packetParser ?? throw new ArgumentNullException(nameof(packetParser));

        if (_header.CodingStyle == null)
        {
            throw new ArgumentException("Header must contain coding style information.", nameof(header));
        }

        _assembler = new JpxSubbandAssembler(_header.CodingStyle);
        _inverseMct = new JpxInverseMct(_header.CodingStyle);
    }

    public JpxTile DecodeTile(JpxTileHeader tileHeader, ReadOnlySpan<byte> tileData, JpxDecodingParameters decodingParameters)
    {
        if (tileHeader == null)
        {
            throw new ArgumentNullException(nameof(tileHeader));
        }

        int decompositionLevels = _header.CodingStyle.DecompositionLevels;
        int levelsToSkip = Math.Min(decodingParameters.LevelsToSkip, decompositionLevels);

        // Compute full-resolution tile dimensions (needed for subband assembly)
        int tileStartX = tileHeader.TileX * (int)_header.TileWidth;
        int tileStartY = tileHeader.TileY * (int)_header.TileHeight;
        int fullTileWidth = Math.Min((int)_header.TileWidth, (int)_header.Width - tileStartX);
        int fullTileHeight = Math.Min((int)_header.TileHeight, (int)_header.Height - tileStartY);

        // Compute reduced tile dimensions (identity when levelsToSkip == 0)
        int reducedWidth = decodingParameters.ReduceDimension(fullTileWidth);
        int reducedHeight = decodingParameters.ReduceDimension(fullTileHeight);

        // Create the output tile with reduced dimensions
        var tile = new JpxTile(_header, tileHeader, reducedWidth, reducedHeight);

        // Decode the tile through the JPEG2000 pipeline
        var reader = new JpxSpanReader(tileData);

        // Skip all marker segments until SOD (Start of Data)
        while (!reader.EndOfSpan && reader.Remaining >= 2)
        {
            ushort marker = reader.PeekUInt16BE();
            if (marker == JpxMarkers.SOD)
            {
                reader.Skip(2);
                break;
            }

            if ((marker & 0xFF00) == 0xFF00 && marker != 0xFF00)
            {
                reader.Skip(2);
                if (reader.Remaining >= 2)
                {
                    ushort segLen = reader.ReadUInt16BE();
                    if (reader.Remaining >= segLen - 2)
                    {
                        reader.Skip(segLen - 2);
                    }
                }
            }
            else
            {
                reader.Skip(1);
            }
        }

        // Stage 1: Parse packets according to progression order
        var packets = _packetParser.ParsePackets(reader.ReadBytes(reader.Remaining), tileHeader);

        // Stage 2: Entropy decode code-blocks using MQ arithmetic decoder (Tier-1)
        // Code-blocks are persistent objects that already accumulated data across all layers
        // during packet parsing (via AppendLayer). Decode each unique block once.
        foreach (var packet in packets)
        {
            if (packet.CodeBlocks == null)
            {
                continue;
            }

            foreach (var codeBlock in packet.CodeBlocks)
            {
                if (codeBlock.Data.Length == 0)
                {
                    continue;
                }

                if (codeBlock.DecodedCoefficients == null)
                {
                    var tier1Decoder = new JpxTier1Decoder(_header.CodingStyle, codeBlock);
                    codeBlock.DecodedCoefficients = tier1Decoder.Decode();
                }
            }
        }

        int decompositionLevelsForDwt = decompositionLevels;

        // Stages 3-5: Assembly → Inverse DWT (with integrated quantization for 9-7)
        for (int component = 0; component < tile.ComponentCount; component++)
        {
            // Stage 3: Assemble code-block coefficients into subbands at full resolution
            var subbands = new JpxSubbandData(fullTileWidth, fullTileHeight, decompositionLevelsForDwt);
            _assembler.Assemble(packets, component, subbands);

            // Stages 4-5: Inverse Quantization + Inverse DWT
            // Dequantization is integrated into the interleaving step of both
            // the 5-3 (reversible) and 9-7 (irreversible) transforms.
            IJpxInverseDwt inverseDwt = JpxInverseDwtFactory.Create(_header, component);
            inverseDwt.Transform(subbands, tile.ComponentData[component], levelsToSkip);
        }

        // Stage 6a: Inverse multi-component transform
        _inverseMct.Apply(tile);

        // Stage 6b: DC level shift and clamp for each component
        for (int component = 0; component < tile.ComponentCount; component++)
        {
            int tileBitDepth = tile.ComponentBitDepths[component];
            bool isSigned = tile.ComponentSigned[component];
            int[] data = tile.ComponentData[component];

            if (!isSigned)
            {
                int dcOffset = 1 << (tileBitDepth - 1);
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] += dcOffset;
                }
            }

            int maxValue = (1 << tileBitDepth) - 1;
            int minValue = isSigned ? -(1 << (tileBitDepth - 1)) : 0;

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] < minValue)
                {
                    data[i] = minValue;
                }
                else if (data[i] > maxValue)
                {
                    data[i] = maxValue;
                }
            }
        }

        return tile;
    }
}
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Decoding;

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
/// <remarks>
/// One decoder serves every tile of an image, and owns the scratch buffers the stages
/// work in so that decoding a tile allocates only the tile it returns. Buffers grow to
/// fit the largest tile seen and are reused from then on.
/// </remarks>
internal sealed class JpxTileDecoder
{
    private readonly JpxHeader _header;
    private readonly JpxCodingStyle _codingStyle;
    private readonly IJpxPacketParser _packetParser;
    private readonly JpxInverseMct _inverseMct;

    // Scratch shared by every tile and component of the image.
    private readonly JpxSubbandData _subbands;
    private readonly JpxDwtScratch _dwtScratch = new();
    private readonly IJpxInverseDwt[] _inverseDwtByComponent;
    private int[] _codeBlockCoefficients = [];
    private uint[] _codeBlockState = [];

    public JpxTileDecoder(JpxHeader header, IJpxPacketParser packetParser)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _packetParser = packetParser ?? throw new ArgumentNullException(nameof(packetParser));

        if (_header.CodingStyle == null)
        {
            throw new ArgumentException("Header must contain coding style information.", nameof(header));
        }

        _codingStyle = _header.CodingStyle;
        _inverseMct = new JpxInverseMct(_codingStyle);
        _subbands = new JpxSubbandData(_codingStyle.DecompositionLevels);

        _inverseDwtByComponent = new IJpxInverseDwt[_header.ComponentCount];
        for (int component = 0; component < _inverseDwtByComponent.Length; component++)
        {
            _inverseDwtByComponent[component] = JpxInverseDwtFactory.Create(_header, component);
        }
    }

    /// <summary>
    /// Decodes a tile-part's codestream into <paramref name="destination"/>, which is
    /// re-targeted at the tile and fully overwritten. <paramref name="observer"/> is notified
    /// as each stage progresses so a long decode can be cancelled promptly.
    /// </summary>
    public void DecodeTile(
        JpxTileHeader tileHeader,
        in ReadOnlySpan<byte> tileData,
        in JpxDecodingParameters decodingParameters,
        JpxTile destination,
        IJpxExectionObserver? observer = default)
    {
        if (tileHeader == null)
        {
            throw new ArgumentNullException(nameof(tileHeader));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        int decompositionLevels = _codingStyle.DecompositionLevels;
        int levelsToSkip = Math.Min(decodingParameters.LevelsToSkip, decompositionLevels);

        // Tile bounds on the reference grid. Subband and code-block partitions are anchored
        // there, so the tile's position matters and not only its size.
        JpxRectangle tileBounds = JpxPacketEnumerationHelper.CalculateTileBounds(_header, tileHeader);

        // Compute reduced tile dimensions (identity when levelsToSkip == 0)
        int reducedWidth = decodingParameters.ReduceDimension(tileBounds.Width);
        int reducedHeight = decodingParameters.ReduceDimension(tileBounds.Height);

        // Target the output tile at the reduced dimensions
        destination.Reset(tileHeader, reducedWidth, reducedHeight);

        // Stage 1: Parse packets according to progression order. Each code-block is
        // returned once, carrying the data accumulated across every layer it appears in.
        IReadOnlyList<JpxCodeBlock> codeBlocks = _packetParser.ParseCodeBlocks(SkipToTileData(tileData), tileHeader);
        observer?.Notify();

        int sampleCount = destination.SampleCount;

        // Reconstructing only down to levelsToSkip leaves the finest resolutions unused, so
        // their code-blocks are never entropy decoded. Resolution r lands on DWT level
        // (levels - r), and the transform reads levels down to levelsToSkip.
        int highestNeededResolution = decompositionLevels - levelsToSkip;

        for (int component = 0; component < destination.ComponentCount; component++)
        {
            _subbands.Reset(tileBounds, levelsToSkip);

            // Stages 2-3: Entropy decode each of this component's code-blocks and place
            // its coefficients into the subband it belongs to.
            bool hasCoefficients = TryDecodeComponentCoefficients(codeBlocks, component, highestNeededResolution, observer);

            // Stages 4-5: Inverse Quantization + Inverse DWT
            // Dequantization is integrated into the interleaving step of both
            // the 5-3 (reversible) and 9-7 (irreversible) transforms.
            // Transforming all-zero subbands only ever produces zeros, which the tile already
            // holds, so a component no code-block contributed to is left as it is.
            if (hasCoefficients)
            {
                _inverseDwtByComponent[component].Transform(_subbands, destination.ComponentData[component].AsSpan(0, sampleCount), _dwtScratch, levelsToSkip);
            }

            observer?.Notify();
        }

        // Stage 6a: Inverse multi-component transform
        _inverseMct.Apply(destination);
        observer?.Notify();

        // Stage 6b: DC level shift and clamp for each component
        for (int component = 0; component < destination.ComponentCount; component++)
        {
            ApplyDcLevelShift(destination.ComponentData[component].AsSpan(0, sampleCount), _header.Components[component]);
            observer?.Notify();
        }
    }

    /// <summary>
    /// Entropy decodes the code-blocks belonging to <paramref name="component"/> that the
    /// inverse transform will read, and places their coefficients into the subbands. Each
    /// block is decoded into shared scratch that the assembler copies out of before the
    /// next block reuses it.
    /// </summary>
    /// <param name="codeBlocks">Every code-block in the tile.</param>
    /// <param name="component">Component whose code-blocks are decoded.</param>
    /// <param name="highestNeededResolution">
    /// Resolutions above this are not reconstructed, so decoding them would be wasted work.
    /// </param>
    /// <param name="observer">Observer notified after each code-block is decoded.</param>
    /// <returns>Whether any code-block contributed coefficients to the component.</returns>
    private bool TryDecodeComponentCoefficients(
        IReadOnlyList<JpxCodeBlock> codeBlocks,
        int component,
        int highestNeededResolution,
        IJpxExectionObserver? observer)
    {
        var placedAnyCoefficients = false;

        for (int i = 0; i < codeBlocks.Count; i++)
        {
            JpxCodeBlock codeBlock = codeBlocks[i];

            if (codeBlock.Component != component || codeBlock.DataOffset == 0)
            {
                continue;
            }

            if (codeBlock.ResolutionLevel > highestNeededResolution)
            {
                continue;
            }

            int sampleCount = codeBlock.Width * codeBlock.Height;
            if (sampleCount <= 0)
            {
                continue;
            }

            int stateLength = JpxTier1Decoder.GetRequiredStateLength(codeBlock.Width, codeBlock.Height);

            if (_codeBlockCoefficients.Length < sampleCount)
            {
                _codeBlockCoefficients = new int[sampleCount];
            }

            if (_codeBlockState.Length < stateLength)
            {
                _codeBlockState = new uint[stateLength];
            }

            Span<int> coefficients = _codeBlockCoefficients.AsSpan(0, sampleCount);
            Span<uint> state = _codeBlockState.AsSpan(0, stateLength);
            coefficients.Clear();
            state.Clear();

            JpxTier1Decoder tier1Decoder = new(_codingStyle, codeBlock, coefficients, state);
            tier1Decoder.Decode();

            JpxSubbandAssembler.Place(codeBlock, coefficients, _subbands);
            placedAnyCoefficients = true;

            observer?.Notify();
        }

        return placedAnyCoefficients;
    }

    /// <summary>
    /// Applies the DC level shift to a component and clamps samples to its nominal range.
    /// </summary>
    private static void ApplyDcLevelShift(in Span<int> data, JpxComponent componentInfo)
    {
        int tileBitDepth = componentInfo.PrecisionBits;
        bool isSigned = componentInfo.IsSigned;
        int maxValue = (1 << tileBitDepth) - 1;
        int minValue = isSigned ? -(1 << (tileBitDepth - 1)) : 0;
        int dcOffset = isSigned ? 0 : (1 << (tileBitDepth - 1));

        for (int i = 0; i < data.Length; i++)
        {
            int value = data[i] + dcOffset;
            if (value < minValue)
            {
                data[i] = minValue;
            }
            else if (value > maxValue)
            {
                data[i] = maxValue;
            }
            else
            {
                data[i] = value;
            }
        }
    }

    /// <summary>
    /// Advances past the tile-part's marker segments to the first byte of packet data,
    /// which follows the SOD (Start of Data) marker.
    /// </summary>
    private static ReadOnlySpan<byte> SkipToTileData(in ReadOnlySpan<byte> tileData)
    {
        JpxSpanReader reader = new(tileData);

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
                    ushort segmentLength = reader.ReadUInt16BE();
                    if (reader.Remaining >= segmentLength - 2)
                    {
                        reader.Skip(segmentLength - 2);
                    }
                }
            }
            else
            {
                reader.Skip(1);
            }
        }

        return reader.ReadBytes(reader.Remaining);
    }
}

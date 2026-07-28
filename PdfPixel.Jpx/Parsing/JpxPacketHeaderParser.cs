using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// JPEG 2000 packet header parser for decoding inclusion, zero bit-plane, and coding pass information.
/// Handles tag-tree decoding and variable-length integer parsing per ITU-T T.800 Annex B.
/// </summary>
internal sealed class JpxPacketHeaderParser
{
    private readonly JpxHeader _header;
    private readonly JpxRectangle _tileBounds;

    // Pre-computed flat array of all precinct states, indexed via resolution/subband offsets
    private readonly JpxPrecinctState[] _precinctStates;

    // Per (resolution, subbandIndex) pair: base offset into _precinctStates, and precinct grid dimensions
    private readonly JpxSubbandLayout[] _subbandLayouts;

    // Strides for component and total subbands
    private readonly int _componentCount;
    private readonly int _totalSubbands; // 1 + 3 * decompositionLevels

    // Every distinct code-block created while parsing this tile's packets, in creation order.
    private readonly List<JpxCodeBlock> _codeBlocks = [];

    // Reused across packets to hold the blocks included in the packet currently being parsed.
    private JpxCodeBlock[] _includedBlocks = [];

    public JpxPacketHeaderParser(JpxHeader header, JpxTileHeader tileHeader)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        if (tileHeader == null)
        {
            throw new ArgumentNullException(nameof(tileHeader));
        }

        if (header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        _tileBounds = JpxPacketEnumerationHelper.CalculateTileBounds(header, tileHeader);
        _componentCount = header.ComponentCount;

        int decompositionLevels = header.CodingStyle.DecompositionLevels;
        int resolutionCount = decompositionLevels + 1;

        // Total subband slots: resolution 0 has 1 subband, resolutions 1..N each have 3
        _totalSubbands = 1 + (3 * decompositionLevels);
        _subbandLayouts = new JpxSubbandLayout[_totalSubbands];

        // Compute layouts and total precinct count
        int totalPrecincts = 0;

        for (int resolution = 0; resolution < resolutionCount; resolution++)
        {
            int subbandCount = (resolution == 0) ? 1 : 3;

            for (int subbandIndex = 0; subbandIndex < subbandCount; subbandIndex++)
            {
                int layoutIndex = GetSubbandLayoutIndex(resolution, subbandIndex);

                (int precinctsX, int precinctsY) = ComputePrecinctGrid(resolution, _tileBounds, header.CodingStyle);

                _subbandLayouts[layoutIndex] = new JpxSubbandLayout
                {
                    BaseOffset = totalPrecincts,
                    PrecinctsX = precinctsX,
                    PrecinctsY = precinctsY,
                    PrecinctStride = precinctsX * precinctsY
                };

                totalPrecincts += _componentCount * precinctsX * precinctsY;
            }
        }

        _precinctStates = new JpxPrecinctState[totalPrecincts];
    }

    /// <summary>
    /// Every distinct code-block encountered while parsing this tile's packets.
    /// Each entry accumulates data across all layers it appears in, so the list is
    /// only complete once every packet has been parsed.
    /// </summary>
    public IReadOnlyList<JpxCodeBlock> CodeBlocks => _codeBlocks;

    /// <summary>
    /// Parses a packet header for the specified precinct and layer, returning the
    /// code-blocks included in this packet. The returned span points into a buffer
    /// reused by the next call and must be consumed before parsing the next packet.
    /// </summary>
    public ReadOnlySpan<JpxCodeBlock> ParsePacketHeader(
    scoped ref JpxBitReader bitReader,
    int layer,
    int resolution,
    int component,
    int precinctX,
    int precinctY)
    {
        // Skip SOP marker if present (ITU-T T.800 Annex A.8.1)
        if (_header.CodingStyle?.HasSopMarkers == true)
        {
            SkipSopMarker(ref bitReader);
        }

        // First bit: packet present (1) or empty (0)
        int packetPresent = bitReader.ReadBit();
        if (packetPresent == 0)
        {
            // Per ITU-T T.800 B.10.7 an empty packet's header is the single zero bit padded
            // to the next byte boundary, and the packet that follows starts there.
            bitReader.ByteAlign();

            // Skip EPH marker if present
            if (_header.CodingStyle?.HasEphMarkers == true)
            {
                SkipEphMarker(ref bitReader);
            }

            return ReadOnlySpan<JpxCodeBlock>.Empty;
        }

        // For resolution 0, there is one subband (LL). For resolution > 0, there are 3 subbands (HL, LH, HH).
        int subbandCount = (resolution == 0) ? 1 : 3;

        // Compute total max code-blocks across all subbands to size the reusable buffer
        int maxCodeBlocks = 0;
        for (int s = 0; s < subbandCount; s++)
        {
            JpxPrecinctState state = GetPrecinctState(resolution, component, precinctX, precinctY, s);
            maxCodeBlocks += state.CodeBlocksX * state.CodeBlocksY;
        }

        if (_includedBlocks.Length < maxCodeBlocks)
        {
            _includedBlocks = new JpxCodeBlock[maxCodeBlocks];
        }

        int count = 0;

        for (int subbandIndex = 0; subbandIndex < subbandCount; subbandIndex++)
        {
            JpxPrecinctState subbandState = GetPrecinctState(resolution, component, precinctX, precinctY, subbandIndex);

            for (int cby = 0; cby < subbandState.CodeBlocksY; cby++)
            {
                for (int cbx = 0; cbx < subbandState.CodeBlocksX; cbx++)
                {
                    JpxCodeBlock? codeBlock = ParseCodeBlockHeader(ref bitReader, subbandState, layer, cbx, cby, component, resolution, subbandIndex);
                    if (codeBlock != null)
                    {
                        _includedBlocks[count++] = codeBlock;
                    }
                }
            }
        }

        // Skip EPH marker if present
        if (_header.CodingStyle?.HasEphMarkers == true)
        {
            SkipEphMarker(ref bitReader);
        }

        // Per ITU-T T.800 B.10.7 the packet body starts on a byte boundary after the header.
        // This applies to every present packet, including one that includes no code-blocks.
        bitReader.ByteAlign();

        return _includedBlocks.AsSpan(0, count);
    }

    /// <summary>
    /// Parses header information for a single code-block per ITU-T T.800 B.10.
    /// Returns the persistent code-block with DataLength set for this layer's contribution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JpxCodeBlock? ParseCodeBlockHeader(
    ref JpxBitReader bitReader,
    JpxPrecinctState state,
    int layer,
    int cbx,
    int cby,
    int component,
    int resolution,
    int subbandIndex)
    {
        bool isFirstInclusion;

        int blockIndex = (cbx * state.CodeBlocksY) + cby;

        if (state.CodeBlocks[blockIndex] != null)
        {
            // Already included in a previous layer — read a single bit for this layer's inclusion
            int included = bitReader.ReadBit();
            if (included == 0)
            {
                return null; // Not included in this layer
            }

            isFirstInclusion = false;
        }
        else
        {
            // Not yet included — use inclusion tag tree with threshold = layer + 1
            bool included = state.InclusionTree.DecodeValue(ref bitReader, cbx, cby, layer + 1);
            if (!included)
            {
                return null; // Not included in this layer
            }

            isFirstInclusion = true;
        }

        // Get or create the persistent code-block for this position
        JpxCodeBlock codeBlock = state.CodeBlocks[blockIndex];
        if (codeBlock == null)
        {
            // The code-block partition is anchored at the subband origin, so this block covers
            // the part of its partition cell that falls inside the precinct-subband intersection.
            int cellX = (state.CodeBlockStartX + cbx) * state.CodeBlockWidth;
            int cellY = (state.CodeBlockStartY + cby) * state.CodeBlockHeight;

            int blockX0 = Math.Max(cellX, state.PrecinctX0);
            int blockY0 = Math.Max(cellY, state.PrecinctY0);
            int blockX1 = Math.Min(cellX + state.CodeBlockWidth, state.PrecinctX1);
            int blockY1 = Math.Min(cellY + state.CodeBlockHeight, state.PrecinctY1);

            codeBlock = new JpxCodeBlock
            {
                SubbandX = blockX0 - state.BandX0,
                SubbandY = blockY0 - state.BandY0,
                Width = blockX1 - blockX0,
                Height = blockY1 - blockY0,
                Component = component,
                ResolutionLevel = resolution,
                SubbandIndex = subbandIndex
            };
            state.CodeBlocks[blockIndex] = codeBlock;
            _codeBlocks.Add(codeBlock);
        }

        // For first inclusion, decode zero bit-planes using tag tree
        if (isFirstInclusion)
        {
            int zeroBitPlanes = state.ZeroBitPlaneTree.DecodeAbsoluteValue(ref bitReader, cbx, cby);
            codeBlock.ZeroBitPlanes = zeroBitPlanes;
        }

        // Read number of coding passes for this layer (ITU-T T.800 Table B.3)
        int codingPasses = ReadCodingPasses(ref bitReader);

        // Read Lblock increment
        while (bitReader.ReadBit() == 1)
        {
            codeBlock.Lblock++;
        }

        // Read the actual data length using Lblock + floor(log2(codingPasses))
        int passExtraBits = 0;
        if (codingPasses > 1)
        {
            passExtraBits = (int)Math.Floor(Math.Log(codingPasses, 2));
        }

        int lengthBitsCount = codeBlock.Lblock + passExtraBits;
        int dataLength = 0;

        if (lengthBitsCount > 0)
        {
            dataLength = (int)bitReader.ReadBits(lengthBitsCount);
        }

        // Store transient per-layer values for body parsing
        codeBlock.DataLength = dataLength;
        codeBlock.LayerCodingPasses = codingPasses;

        return codeBlock;
    }

    /// <summary>
    /// Reads the number of coding passes per ITU-T T.800 Table B.3.
    /// Encoding: 0→1, 10→2, 11+2bits(00-10)→3-5, 1111+5bits(00000-11110)→6-36, 1111 11111+7bits→37-164.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadCodingPasses(ref JpxBitReader bitReader)
    {
        // 0 → 1 pass
        if (bitReader.ReadBit() == 0)
        {
            return 1;
        }

        // 10 → 2 passes
        if (bitReader.ReadBit() == 0)
        {
            return 2;
        }

        // 11 + 2 bits
        var twoExtraBits = (int)bitReader.ReadBits(2);
        if (twoExtraBits < 3)
        {
            // 1100→3, 1101→4, 1110→5
            return 3 + twoExtraBits;
        }

        // 1111 + 5 bits
        var fiveExtraBits = (int)bitReader.ReadBits(5);
        if (fiveExtraBits < 31)
        {
            return 6 + fiveExtraBits;
        }

        // 1111 11111 + 7 bits → 37 to 164
        var sevenExtraBits = (int)bitReader.ReadBits(7);
        return 37 + sevenExtraBits;
    }

    /// <summary>
    /// Gets or creates per-precinct state using direct array indexing.
    /// Creates state lazily on first access with pre-computed code-block grid dimensions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JpxPrecinctState GetPrecinctState(int resolution, int component, int precinctX, int precinctY, int subbandIndex)
    {
        if (_header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        int layoutIndex = GetSubbandLayoutIndex(resolution, subbandIndex);
        ref JpxSubbandLayout layout = ref _subbandLayouts[layoutIndex];
        int flatIndex = layout.BaseOffset + (component * layout.PrecinctStride) + (precinctY * layout.PrecinctsX) + precinctX;

        JpxPrecinctState state = _precinctStates[flatIndex];
        if (state != null)
        {
            return state;
        }

        state = JpxPrecinctState.Create(resolution, subbandIndex, precinctX, precinctY, _tileBounds, _header.CodingStyle);
        _precinctStates[flatIndex] = state;
        return state;
    }

    /// <summary>
    /// Computes the linear index into <see cref="_subbandLayouts"/> for a given (resolution, subbandIndex) pair.
    /// Resolution 0 occupies index 0; resolution r > 0 occupies indices 1 + (r-1)*3 + subbandIndex.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSubbandLayoutIndex(int resolution, int subbandIndex) => (resolution == 0) ? 0 : 1 + ((resolution - 1) * 3) + subbandIndex;

    /// <summary>
    /// Computes the precinct grid dimensions for a resolution level from the tile's bounds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int precinctsX, int precinctsY) ComputePrecinctGrid(
    int resolution,
    in JpxRectangle tileBounds,
    JpxCodingStyle codingStyle)
    {
        (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
            tileBounds, resolution, codingStyle);

        return (Math.Max(precinctsX, 1), Math.Max(precinctsY, 1));
    }

    /// <summary>
    /// Skips SOP marker (0xFF91 + 4-byte body) per ITU-T T.800 Annex A.8.1.
    /// When the coding style signals SOP markers, they are unconditionally present
    /// before each packet. The marker is 6 bytes: FF 91 00 04 Nsop(2 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipSopMarker(ref JpxBitReader bitReader)
    {
        bitReader.ByteAlign();

        // SOP marker is 6 bytes total: FF 91 00 04 Nsop_high Nsop_low
        // When HasSopMarkers is true, the marker is guaranteed to be present.
        // Skip all 6 bytes (marker code + Lsop + Nsop).
        bitReader.ReadRawSpan(6);
    }

    /// <summary>
    /// Skips EPH marker (0xFF92, no body) per ITU-T T.800 Annex A.8.2.
    /// When the coding style signals EPH markers, they are unconditionally present
    /// after each packet header. The marker is 2 bytes: FF 92.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipEphMarker(ref JpxBitReader bitReader)
    {
        bitReader.ByteAlign();

        // EPH marker is 2 bytes: FF 92
        // When HasEphMarkers is true, the marker is guaranteed to be present.
        bitReader.ReadRawSpan(2);
    }

    /// <summary>
    /// Calculates the width of the current tile, accounting for edge tiles.
    /// Per ITU-T T.800, the last tile column may be narrower than the nominal tile width.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateTileWidth(JpxHeader header, JpxTileHeader tileHeader)
    {
        int nominalWidth = (header.TileWidth > 0)
            ? (int)header.TileWidth
            : (int)header.Width / Math.Max(tileHeader.TilesHorizontal, 1);

        int tileX = tileHeader.TileX;
        int tileStartX = tileX * nominalWidth;
        int tileEndX = Math.Min(tileStartX + nominalWidth, (int)header.Width);

        return tileEndX - tileStartX;
    }

    /// <summary>
    /// Calculates the height of the current tile, accounting for edge tiles.
    /// Per ITU-T T.800, the last tile row may be shorter than the nominal tile height.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateTileHeight(JpxHeader header, JpxTileHeader tileHeader)
    {
        int nominalHeight = (header.TileHeight > 0)
            ? (int)header.TileHeight
            : (int)header.Height / Math.Max(tileHeader.TilesVertical, 1);

        int tileY = tileHeader.TileY;
        int tileStartY = tileY * nominalHeight;
        int tileEndY = Math.Min(tileStartY + nominalHeight, (int)header.Height);

        return tileEndY - tileStartY;
    }

    }

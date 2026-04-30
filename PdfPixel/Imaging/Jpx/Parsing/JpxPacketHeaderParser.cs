using PdfPixel.Imaging.Jpx.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jpx.Parsing;

/// <summary>
/// JPEG 2000 packet header parser for decoding inclusion, zero bit-plane, and coding pass information.
/// Handles tag-tree decoding and variable-length integer parsing per ITU-T T.800 Annex B.
/// </summary>
internal sealed class JpxPacketHeaderParser
{
    private readonly JpxHeader _header;
    private readonly int _tileWidth;
    private readonly int _tileHeight;

    // Pre-computed flat array of all precinct states, indexed via resolution/subband offsets
    private readonly JpxPrecinctState[] _precinctStates;

    // Per (resolution, subbandIndex) pair: base offset into _precinctStates, and precinct grid dimensions
    private readonly JpxSubbandLayout[] _subbandLayouts;

    // Strides for component and total subbands
    private readonly int _componentCount;
    private readonly int _totalSubbands; // 1 + 3 * decompositionLevels

    /// <summary>
    /// Represents parsing state for a single packet header.
    /// </summary>
    public struct PacketHeaderInfo
    {
        public bool IsEmpty;
        public JpxCodeBlock[] CodeBlocks;
        public int HeaderLengthBits;
    }

    public JpxPacketHeaderParser(JpxHeader header, JpxTileHeader tileHeader)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        if (tileHeader == null)
        {
            throw new ArgumentNullException(nameof(tileHeader));
        }

        _tileWidth = CalculateTileWidth(header, tileHeader);
        _tileHeight = CalculateTileHeight(header, tileHeader);
        _componentCount = header.ComponentCount;

        int decompositionLevels = header.CodingStyle.DecompositionLevels;
        int resolutionCount = decompositionLevels + 1;

        // Total subband slots: resolution 0 has 1 subband, resolutions 1..N each have 3
        _totalSubbands = 1 + 3 * decompositionLevels;
        _subbandLayouts = new JpxSubbandLayout[_totalSubbands];

        // Compute layouts and total precinct count
        int totalPrecincts = 0;

        for (int resolution = 0; resolution < resolutionCount; resolution++)
        {
            int subbandCount = (resolution == 0) ? 1 : 3;

            for (int subbandIndex = 0; subbandIndex < subbandCount; subbandIndex++)
            {
                int layoutIndex = GetSubbandLayoutIndex(resolution, subbandIndex);

                var (precinctsX, precinctsY) = ComputePrecinctGrid(
                    resolution, _tileWidth, _tileHeight, header.CodingStyle);

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
    /// Parses a packet header for the specified precinct and layer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketHeaderInfo ParsePacketHeader(
        ref JpxBitReader bitReader,
        int layer,
        int resolution,
        int component,
        int precinctX,
        int precinctY)
    {
        var headerInfo = new PacketHeaderInfo();
        int startBitPosition = bitReader.BitsConsumed;

        // Skip SOP marker if present (ITU-T T.800 Annex A.8.1)
        if (_header.CodingStyle?.HasSopMarkers == true)
        {
            SkipSopMarker(ref bitReader);
        }

        // First bit: packet present (1) or empty (0)
        int packetPresent = bitReader.ReadBit();
        if (packetPresent == 0)
        {
            headerInfo.IsEmpty = true;
            headerInfo.CodeBlocks = Array.Empty<JpxCodeBlock>();
            headerInfo.HeaderLengthBits = bitReader.BitsConsumed - startBitPosition;

            // Skip EPH marker if present
            if (_header.CodingStyle?.HasEphMarkers == true)
            {
                SkipEphMarker(ref bitReader);
            }

            return headerInfo;
        }

        // For resolution 0, there is one subband (LL). For resolution > 0, there are 3 subbands (HL, LH, HH).
        int subbandCount = (resolution == 0) ? 1 : 3;

        // Compute total max code-blocks across all subbands to pre-allocate output array
        int maxCodeBlocks = 0;
        for (int s = 0; s < subbandCount; s++)
        {
            var state = GetPrecinctState(resolution, component, precinctX, precinctY, s);
            maxCodeBlocks += state.CodeBlocksX * state.CodeBlocksY;
        }

        var codeBlocks = new JpxCodeBlock[maxCodeBlocks];
        int count = 0;

        for (int subbandIndex = 0; subbandIndex < subbandCount; subbandIndex++)
        {
            var subbandState = GetPrecinctState(resolution, component, precinctX, precinctY, subbandIndex);

            for (int cby = 0; cby < subbandState.CodeBlocksY; cby++)
            {
                for (int cbx = 0; cbx < subbandState.CodeBlocksX; cbx++)
                {
                    var codeBlock = ParseCodeBlockHeader(ref bitReader, subbandState, layer, cbx, cby);
                    if (codeBlock != null)
                    {
                        codeBlock.SubbandIndex = subbandIndex;
                        codeBlock.ResolutionLevel = resolution;
                        codeBlocks[count++] = codeBlock;
                    }
                }
            }
        }

        // Skip EPH marker if present
        if (_header.CodingStyle?.HasEphMarkers == true)
        {
            SkipEphMarker(ref bitReader);
        }

        headerInfo.IsEmpty = false;

        if (count < maxCodeBlocks)
        {
            Array.Resize(ref codeBlocks, count);
        }

        headerInfo.CodeBlocks = codeBlocks;
        headerInfo.HeaderLengthBits = bitReader.BitsConsumed - startBitPosition;

        return headerInfo;
    }

    /// <summary>
    /// Parses header information for a single code-block per ITU-T T.800 B.10.
    /// Returns the persistent code-block with DataLength set for this layer's contribution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JpxCodeBlock ParseCodeBlockHeader(
        ref JpxBitReader bitReader,
        JpxPrecinctState state,
        int layer,
        int cbx,
        int cby)
    {
        bool isFirstInclusion;

        int blockIndex = cbx * state.CodeBlocksY + cby;

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
        var codeBlock = state.CodeBlocks[blockIndex];
        if (codeBlock == null)
        {
            int nominalCbW = _header.CodingStyle?.CodeBlockWidth ?? 64;
            int nominalCbH = _header.CodingStyle?.CodeBlockHeight ?? 64;

            // Compute actual code-block dimensions clipped to subband bounds
            int absX = state.CodeBlockStartX + cbx;
            int absY = state.CodeBlockStartY + cby;
            int cbPixelX = absX * nominalCbW;
            int cbPixelY = absY * nominalCbH;
            int actualWidth = Math.Min(nominalCbW, state.SubbandX1 - cbPixelX);
            int actualHeight = Math.Min(nominalCbH, state.SubbandY1 - cbPixelY);

            codeBlock = new JpxCodeBlock
            {
                X = absX,
                Y = absY,
                Width = actualWidth,
                Height = actualHeight
            };
            state.CodeBlocks[blockIndex] = codeBlock;
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
        int twoExtraBits = (int)bitReader.ReadBits(2);
        if (twoExtraBits < 3)
        {
            // 1100→3, 1101→4, 1110→5
            return 3 + twoExtraBits;
        }

        // 1111 + 5 bits
        int fiveExtraBits = (int)bitReader.ReadBits(5);
        if (fiveExtraBits < 31)
        {
            return 6 + fiveExtraBits;
        }

        // 1111 11111 + 7 bits → 37 to 164
        int sevenExtraBits = (int)bitReader.ReadBits(7);
        return 37 + sevenExtraBits;
    }

    /// <summary>
    /// Gets or creates per-precinct state using direct array indexing.
    /// Creates state lazily on first access with pre-computed code-block grid dimensions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JpxPrecinctState GetPrecinctState(int resolution, int component, int precinctX, int precinctY, int subbandIndex)
    {
        int layoutIndex = GetSubbandLayoutIndex(resolution, subbandIndex);
        ref JpxSubbandLayout layout = ref _subbandLayouts[layoutIndex];
        int flatIndex = layout.BaseOffset + component * layout.PrecinctStride + precinctY * layout.PrecinctsX + precinctX;

        var state = _precinctStates[flatIndex];
        if (state != null)
        {
            return state;
        }

        state = JpxPrecinctState.Create(resolution, subbandIndex, precinctX, precinctY, _tileWidth, _tileHeight, _header.CodingStyle);
        _precinctStates[flatIndex] = state;
        return state;
    }

    /// <summary>
    /// Computes the linear index into <see cref="_subbandLayouts"/> for a given (resolution, subbandIndex) pair.
    /// Resolution 0 occupies index 0; resolution r > 0 occupies indices 1 + (r-1)*3 + subbandIndex.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSubbandLayoutIndex(int resolution, int subbandIndex)
    {
        return (resolution == 0) ? 0 : 1 + (resolution - 1) * 3 + subbandIndex;
    }

    /// <summary>
    /// Computes the precinct grid dimensions for a resolution level using tile dimensions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int precinctsX, int precinctsY) ComputePrecinctGrid(
        int resolution,
        int tileWidth,
        int tileHeight,
        JpxCodingStyle codingStyle)
    {
        var (precinctsX, precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
            tileWidth, tileHeight, resolution, codingStyle);

        return (Math.Max(precinctsX, 1), Math.Max(precinctsY, 1));
    }

    /// <summary>
    /// Skips SOP marker if present (0xFF91 + 4-byte body).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipSopMarker(ref JpxBitReader bitReader)
    {
        bitReader.ByteAlign();

        // SOP is 6 bytes total: FF 91 00 04 Nsop(2 bytes)
        // We'd need to peek to confirm, but for simplicity align and skip if matching
        // TODO: Implement proper SOP marker detection
    }

    /// <summary>
    /// Skips EPH marker if present (0xFF92, no body).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipEphMarker(ref JpxBitReader bitReader)
    {
        bitReader.ByteAlign();
        // EPH is 2 bytes: FF 92
        // TODO: Implement proper EPH marker detection
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

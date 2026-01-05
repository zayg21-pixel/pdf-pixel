using PdfReader.Imaging.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfReader.Imaging.Jpx.Parsing;

/// <summary>
/// JPEG 2000 packet header parser for decoding inclusion, zero bit-plane, and coding pass information.
/// Handles tag-tree decoding and variable-length integer parsing according to JPEG 2000 standard.
/// </summary>
internal sealed class JpxPacketHeaderParser
{
    private readonly JpxHeader _header;
    private readonly JpxTileHeader _tileHeader;
    
    // Tag trees for inclusion and zero bit-plane information - shared across packets
    private JpxTagTree _inclusionTree;
    private JpxTagTree _zeroBitPlaneTree;
    
    // Track which code-blocks have been included in previous layers
    private bool[,] _codeBlockIncluded;
    private int[,] _codeBlockFirstInclusionLayer;
    
    // Cached precinct dimensions to avoid recalculation
    private int _lastResolution = -1;
    private int _lastComponent = -1;
    private int _precinctWidth;
    private int _precinctHeight;
    private int _codeBlocksX;
    private int _codeBlocksY;

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
        _tileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
    }

    /// <summary>
    /// Parses a packet header for the specified precinct and layer.
    /// </summary>
    /// <param name="bitReader">Bit reader positioned at the start of the packet header.</param>
    /// <param name="layer">Quality layer index.</param>
    /// <param name="resolution">Resolution level.</param>
    /// <param name="component">Component index.</param>
    /// <param name="precinctX">Precinct X coordinate.</param>
    /// <param name="precinctY">Precinct Y coordinate.</param>
    /// <returns>Parsed packet header information.</returns>
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
        
        // Initialize precinct parameters if needed (cache to avoid recalculation)
        EnsurePrecinctParameters(resolution, component);

        // Stage 3a: Check if packet is empty (first bit indicates presence)
        int packetPresent = bitReader.ReadBit();
        if (packetPresent == 0)
        {
            headerInfo.IsEmpty = true;
            headerInfo.CodeBlocks = Array.Empty<JpxCodeBlock>();
            headerInfo.HeaderLengthBits = bitReader.BitsConsumed - startBitPosition;
            return headerInfo;
        }

        // Packet contains data, parse code-block information
        var codeBlocks = new List<JpxCodeBlock>();

        // Stage 3b: Iterate through code-blocks in this precinct
        for (int cby = 0; cby < _codeBlocksY; cby++)
        {
            for (int cbx = 0; cbx < _codeBlocksX; cbx++)
            {
                // Parse code-block header
                var codeBlock = ParseCodeBlockHeader(ref bitReader, layer, cbx, cby);
                if (codeBlock != null)
                {
                    codeBlocks.Add(codeBlock);
                }
            }
        }

        headerInfo.IsEmpty = false;
        headerInfo.CodeBlocks = codeBlocks.ToArray();
        headerInfo.HeaderLengthBits = bitReader.BitsConsumed - startBitPosition;
        
        return headerInfo;
    }

    /// <summary>
    /// Parses header information for a single code-block.
    /// </summary>
    private JpxCodeBlock ParseCodeBlockHeader(ref JpxBitReader bitReader, int layer, int cbx, int cby)
    {
        // Stage 3c: Check inclusion using tag-tree
        bool isIncluded = CheckInclusion(ref bitReader, cbx, cby, layer);
        if (!isIncluded)
        {
            return null; // Code-block not included in this layer
        }

        var codeBlock = new JpxCodeBlock
        {
            X = cbx,
            Y = cby
        };

        // Stage 3d: For first inclusion, read zero bit-planes
        if (layer == 0) // First layer where this code-block is included
        {
            int zbp = ReadZeroBitPlanes(ref bitReader, cbx, cby);
            codeBlock.ZeroBitPlanes = zbp;
        }

        // Stage 3e: Read number of coding passes
        int codingPasses = ReadCodingPasses(ref bitReader);
        codeBlock.CodingPasses = codingPasses;

        // Stage 3f: Read length information (proper JPEG2000 variable-length encoding)
        int lengthBytes = ReadCodeBlockLength(ref bitReader);
        codeBlock.DataLength = lengthBytes;
        
        return codeBlock;
    }

    /// <summary>
    /// Checks code-block inclusion using the tag-tree.
    /// </summary>
    private bool CheckInclusion(ref JpxBitReader bitReader, int cbx, int cby, int layer)
    {
        // Check if already included in a previous layer
        if (_codeBlockIncluded != null && _codeBlockIncluded[cbx, cby])
        {
            return true; // Already included in previous layer, continue to include
        }
        
        if (_inclusionTree == null)
        {
            // No tag tree - assume all code-blocks are included starting from layer 0
            if (_codeBlockIncluded != null && layer == 0)
            {
                _codeBlockIncluded[cbx, cby] = true;
                _codeBlockFirstInclusionLayer[cbx, cby] = 0;
            }
            return _codeBlockIncluded?[cbx, cby] ?? true;
        }

        // Use tag tree to determine if this code-block should be included in this layer
        // The tag tree encodes the first layer where each code-block appears
        bool includeInThisLayer = _inclusionTree.DecodeValue(ref bitReader, cbx, cby, layer);
        
        if (includeInThisLayer && _codeBlockIncluded != null && !_codeBlockIncluded[cbx, cby])
        {
            // First time including this code-block
            _codeBlockIncluded[cbx, cby] = true;
            _codeBlockFirstInclusionLayer[cbx, cby] = layer;
        }
        
        return includeInThisLayer || (_codeBlockIncluded?[cbx, cby] ?? false);
    }

    /// <summary>
    /// Reads zero bit-plane information using the tag-tree.
    /// </summary>
    private int ReadZeroBitPlanes(ref JpxBitReader bitReader, int cbx, int cby)
    {
        if (_zeroBitPlaneTree == null)
        {
            return 0; // Default to no zero bit-planes
        }

        // Decode zero bit-planes using tag-tree
        // The value represents the number of zero bit-planes
        for (int zbp = 0; zbp < 8; zbp++)
        {
            if (_zeroBitPlaneTree.DecodeValue(ref bitReader, cbx, cby, zbp))
            {
                return zbp;
            }
        }
        
        return 0; // Fallback - no zero bit-planes
    }

    /// <summary>
    /// Reads the number of coding passes for the current code-block.
    /// Uses proper JPEG2000 variable-length encoding.
    /// </summary>
    private int ReadCodingPasses(ref JpxBitReader bitReader)
    {
        // JPEG2000 coding pass encoding:
        // 1 pass: 0
        // 2 passes: 10
        // 3 passes: 110
        // 4 passes: 111 + 2 bits
        // etc.
        
        if (bitReader.ReadBit() == 0)
        {
            return 1;
        }
        
        if (bitReader.ReadBit() == 0)
        {
            return 2;
        }
        
        if (bitReader.ReadBit() == 0)
        {
            return 3;
        }
        
        // More than 3 passes - read additional bits
        int extraBits = (int)bitReader.ReadBits(2);
        return 4 + extraBits;
    }

    /// <summary>
    /// Reads the length of code-block data using proper JPEG2000 variable-length encoding.
    /// </summary>
    private int ReadCodeBlockLength(ref JpxBitReader bitReader)
    {
        // JPEG2000 length encoding uses variable-length integers
        // Read bits until we get a 0, then read that many bits for the actual length
        
        int lengthBits = 0;
        
        // Count leading 1s to determine number of bits to read
        while (bitReader.ReadBit() == 1)
        {
            lengthBits++;
            if (lengthBits > 16) // Safety limit
            {
                break;
            }
        }
        
        if (lengthBits == 0)
        {
            return 0; // No data for this code-block in this layer
        }
        
        // Read the actual length value
        int length = (int)bitReader.ReadBits(lengthBits);
        
        // JPEG2000 length encoding: actual length is length + 1
        // (0 bits = 1 byte, 1 bit = 2 bytes, etc.)
        return length + 1;
    }

    /// <summary>
    /// Initializes precinct parameters for the given resolution and component.
    /// Uses caching to avoid recalculation for the same resolution/component.
    /// </summary>
    private void EnsurePrecinctParameters(int resolution, int component)
    {
        // Check if we need to recalculate (resolution or component changed)
        if (_lastResolution == resolution && _lastComponent == component && _inclusionTree != null)
        {
            return; // Use cached values
        }

        _lastResolution = resolution;
        _lastComponent = component;

        // Calculate tile dimensions
        int tileWidth = CalculateTileWidth();
        int tileHeight = CalculateTileHeight();

        // Get precinct dimensions
        var (precinctWidth, precinctHeight) = JpxPrecinctHelper.GetPrecinctSize(
            resolution, _header.CodingStyle);
        
        _precinctWidth = precinctWidth;
        _precinctHeight = precinctHeight;

        // Calculate code-block grid within precinct
        var (codeBlocksX, codeBlocksY) = JpxPrecinctHelper.GetCodeBlockGrid(
            precinctWidth, precinctHeight, _header.CodingStyle);
        
        _codeBlocksX = codeBlocksX;
        _codeBlocksY = codeBlocksY;

        // Initialize tag trees and inclusion tracking only once per resolution/component
        if (_inclusionTree == null && codeBlocksX > 0 && codeBlocksY > 0)
        {
            _inclusionTree = new JpxTagTree(codeBlocksX, codeBlocksY);
            _zeroBitPlaneTree = new JpxTagTree(codeBlocksX, codeBlocksY);
            _codeBlockIncluded = new bool[codeBlocksX, codeBlocksY];
            _codeBlockFirstInclusionLayer = new int[codeBlocksX, codeBlocksY];
        }
    }

    /// <summary>
    /// Calculates the width of the current tile.
    /// </summary>
    private int CalculateTileWidth()
    {
        if (_header.TileWidth > 0)
        {
            return (int)Math.Min(_header.TileWidth, _header.Width);
        }
        return (int)_header.Width / Math.Max(_tileHeader.TilesHorizontal, 1);
    }

    /// <summary>
    /// Calculates the height of the current tile.
    /// </summary>
    private int CalculateTileHeight()
    {
        if (_header.TileHeight > 0)
        {
            return (int)Math.Min(_header.TileHeight, _header.Height);
        }
        return (int)_header.Height / Math.Max(_tileHeader.TilesVertical, 1);
    }

    /// <summary>
    /// Resets the tag trees for a new tile or precinct.
    /// </summary>
    public void Reset()
    {
        _inclusionTree?.Reset();
        _zeroBitPlaneTree?.Reset();
        
        // Reset inclusion tracking
        if (_codeBlockIncluded != null)
        {
            Array.Clear(_codeBlockIncluded, 0, _codeBlockIncluded.Length);
            Array.Clear(_codeBlockFirstInclusionLayer, 0, _codeBlockFirstInclusionLayer.Length);
        }
    }
}
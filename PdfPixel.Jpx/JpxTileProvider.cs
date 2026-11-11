using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Packets;
using PdfPixel.Jpx.Parsing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Jpx;

/// <summary>
/// Provides on-demand decoding of individual JPX tiles. Tile extraction (Phase 1)
/// is performed eagerly in the constructor; actual tile decoding is deferred until
/// <see cref="DecodeTile"/> is called.
/// </summary>
public readonly struct JpxTileProvider
{
    private readonly JpxExtractedTileContent[] _extractedTiles;
    private readonly JpxTileDecoder _tileDecoder;
    private readonly JpxHeader _header;
    private readonly JpxDecodingParameters _decodingParameters;

    /// <summary>
    /// Initializes a new <see cref="JpxTileProvider"/> by extracting tile contents from the codestream.
    /// </summary>
    /// <param name="header">Parsed JPX header containing image and tile grid metadata.</param>
    /// <param name="codestream">Raw codestream data starting from the first SOT marker.</param>
    /// <param name="decodingParameters">Parameters controlling decoding resolution.</param>
    public JpxTileProvider(
        JpxHeader header,
        in ReadOnlyMemory<byte> codestream,
        in JpxDecodingParameters decodingParameters = default)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _decodingParameters = (decodingParameters.DescaleFactor >= 1) ? decodingParameters : JpxDecodingParameters.Default;
        JpxTilePartExtractor tileContentExtractor = new();

        if (header.CodingStyle == null)
        {
            throw new NotImplementedException("JPX images without coding style information are not supported.");
        }

        var progressionOrder = (JpxProgressionOrder)header.CodingStyle.ProgressionOrder;
        IJpxPacketParser packetParser = JpxPacketParserFactory.CreateParser(progressionOrder, header);
        ComponentSelection = ResolveComponentSelection(header, _decodingParameters);
        _tileDecoder = new JpxTileDecoder(header, packetParser, ComponentSelection);

        TilesHorizontal = (int)Math.Ceiling((double)header.Width / header.TileWidth);
        TilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight);
        TotalTiles = TilesHorizontal * TilesVertical;

        _extractedTiles = tileContentExtractor.ExtractTileContents(header, codestream);
    }

    /// <summary>
    /// Gets the number of tiles horizontally in the tile grid.
    /// </summary>
    public int TilesHorizontal { get; }

    /// <summary>
    /// Gets the number of tiles vertically in the tile grid.
    /// </summary>
    public int TilesVertical { get; }

    /// <summary>
    /// Gets the total number of tiles in the image.
    /// </summary>
    public int TotalTiles { get; }

    /// <summary>
    /// Gets the indices of the components reconstructed by this provider, in codestream order.
    /// </summary>
    public IReadOnlyList<int> ComponentSelection { get; }

    /// <summary>
    /// Decodes a single tile by its index into <paramref name="destination"/>, which is
    /// re-targeted at that tile. Produces a zeroed tile when no data is present in the
    /// codestream for the requested index.
    /// </summary>
    /// <param name="tileIndex">Zero-based tile index in raster order.</param>
    /// <param name="destination">Tile storage to decode into.</param>
    /// <param name="observer">Observer notified as decoding progresses.</param>
    /// <exception cref="InvalidDataException">Thrown when tile decoding fails.</exception>
    public void DecodeTile(int tileIndex, JpxTile destination, IJpxExecutionObserver? observer = default)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        JpxExtractedTileContent extracted = _extractedTiles[tileIndex];

        if (extracted.TileHeader == null)
        {
            ResetToEmptyTile(tileIndex, destination);
            return;
        }

        _tileDecoder.DecodeTile(extracted.TileHeader, extracted.Data.Span, _decodingParameters, destination, observer);
    }

    private void ResetToEmptyTile(int tileIndex, JpxTile destination)
    {
        JpxTileHeader tileHeader = new()
        {
            TileIndex = (ushort)tileIndex,
            TilePartLength = 0,
            TilePartIndex = 0,
            TilePartCount = 1,
            TilesHorizontal = TilesHorizontal,
            TilesVertical = TilesVertical
        };

        int tileStartX = tileHeader.TileX * (int)_header.TileWidth;
        int tileStartY = tileHeader.TileY * (int)_header.TileHeight;
        int fullW = Math.Min((int)_header.TileWidth, (int)_header.Width - tileStartX);
        int fullH = Math.Min((int)_header.TileHeight, (int)_header.Height - tileStartY);
        int reducedW = _decodingParameters.ReduceDimension(fullW);
        int reducedH = _decodingParameters.ReduceDimension(fullH);

        // Reset zeroes the component data, which is the whole content of an absent tile
        destination.Reset(tileHeader, reducedW, reducedH);
    }

    /// <summary>
    /// Creates tile storage sized for this image's selected components, suitable for passing to
    /// <see cref="DecodeTile"/> and reusing across tiles.
    /// </summary>
    public JpxTile CreateTile() => new(_header, ComponentSelection);

    /// <summary>
    /// Resolves which components are reconstructed: the parameters' selection, or every component with the cdef
    /// opacity component last. A selection holding some but not all of the components the multiple component
    /// transform combines is rejected (ITU-T T.800 Annex G).
    /// </summary>
    private static int[] ResolveComponentSelection(JpxHeader header, in JpxDecodingParameters decodingParameters)
    {
        IReadOnlyList<int>? requestedSelection = decodingParameters.ComponentSelection;

        if (requestedSelection == null)
        {
            return CreateDefaultComponentSelection(header);
        }

        var selection = new int[requestedSelection.Count];
        int transformedCount = 0;

        for (int index = 0; index < selection.Length; index++)
        {
            int component = requestedSelection[index];

            if (component < 0 || component >= header.ComponentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(decodingParameters), $"Component {component} is outside the {header.ComponentCount} components of the codestream.");
            }

            if (component < 3)
            {
                transformedCount++;
            }

            selection[index] = component;
        }

        bool hasTransform = header.CodingStyle != null && header.CodingStyle.MultiComponentTransform != 0 && header.ComponentCount >= 3;

        if (hasTransform && transformedCount != 0 && transformedCount != 3)
        {
            throw new ArgumentException("A component selection takes all or none of the first three components when the multiple component transform is on.", nameof(decodingParameters));
        }

        return selection;
    }

    private static int[] CreateDefaultComponentSelection(JpxHeader header)
    {
        int opacityComponentIndex = header.OpacityComponentIndex;
        var selection = new int[header.ComponentCount];
        int selectionIndex = 0;

        for (int component = 0; component < header.ComponentCount; component++)
        {
            if (component != opacityComponentIndex)
            {
                selection[selectionIndex++] = component;
            }
        }

        if (opacityComponentIndex >= 0)
        {
            selection[selectionIndex] = opacityComponentIndex;
        }

        return selection;
    }
}

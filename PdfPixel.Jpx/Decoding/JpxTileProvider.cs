using PdfPixel.Jpx.Model;
using System;
using System.IO;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Provides on-demand decoding of individual JPX tiles. Tile extraction (Phase 1)
/// is performed eagerly in the constructor; actual tile decoding is deferred until
/// <see cref="DecodeTile"/> is called.
/// </summary>
public readonly struct JpxTileProvider
{
    private readonly ExtractedTileContent[] _extractedTiles;
    private readonly IJpxTileDecoder _tileDecoder;
    private readonly JpxHeader _header;
    private readonly JpxDecodingParameters _decodingParameters;

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
    /// Initializes a new <see cref="JpxTileProvider"/> by extracting tile contents from the codestream.
    /// </summary>
    /// <param name="header">Parsed JPX header containing image and tile grid metadata.</param>
    /// <param name="codestream">Raw codestream data starting from the first SOT marker.</param>
    /// <param name="tileDecoder">Decoder used to decode individual tiles on demand.</param>
    /// <param name="decodingParameters">Parameters controlling decoding resolution.</param>
    public JpxTileProvider(JpxHeader header, ReadOnlySpan<byte> codestream, IJpxTileDecoder tileDecoder, JpxDecodingParameters decodingParameters = default)
        : this(header, codestream, tileDecoder, new TileContentExtractor(), decodingParameters)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="JpxTileProvider"/> by extracting tile contents from the codestream.
    /// </summary>
    /// <param name="header">Parsed JPX header containing image and tile grid metadata.</param>
    /// <param name="codestream">Raw codestream data starting from the first SOT marker.</param>
    /// <param name="tileDecoder">Decoder used to decode individual tiles on demand.</param>
    /// <param name="tileContentExtractor">Extractor used to parse tile-part data from the codestream.</param>
    /// <param name="decodingParameters">Parameters controlling decoding resolution.</param>
    public JpxTileProvider(
        JpxHeader header,
        ReadOnlySpan<byte> codestream,
        IJpxTileDecoder tileDecoder,
        ITileContentExtractor tileContentExtractor,
        JpxDecodingParameters decodingParameters = default)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _tileDecoder = tileDecoder ?? throw new ArgumentNullException(nameof(tileDecoder));
        _decodingParameters = decodingParameters.DescaleFactor >= 1 ? decodingParameters : JpxDecodingParameters.Default;

        if (tileContentExtractor == null)
        {
            throw new ArgumentNullException(nameof(tileContentExtractor));
        }

        TilesHorizontal = (int)Math.Ceiling((double)header.Width / header.TileWidth);
        TilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight);
        TotalTiles = TilesHorizontal * TilesVertical;

        // Phase 1: Extract concatenated tile data from codestream (essentially free)
        _extractedTiles = tileContentExtractor.ExtractTileContents(header, codestream);
    }

    /// <summary>
    /// Decodes a single tile by its index. Returns an empty tile when no data is present
    /// in the codestream for the requested index.
    /// </summary>
    /// <param name="tileIndex">Zero-based tile index in raster order.</param>
    /// <returns>The decoded <see cref="JpxTile"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when tile decoding fails.</exception>
    public JpxTile DecodeTile(int tileIndex)
    {
        var extracted = _extractedTiles[tileIndex];

        if (extracted.Data == null)
        {
            return CreateEmptyTile(tileIndex);
        }

        return _tileDecoder.DecodeTile(extracted.TileHeader, extracted.Data, _decodingParameters);
    }

    private JpxTile CreateEmptyTile(int tileIndex)
    {
        var tileHeader = new JpxTileHeader
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

        // Component data arrays are automatically initialized to zeros by the constructor
        return new JpxTile(_header, tileHeader, reducedW, reducedH);
    }
}

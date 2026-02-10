using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Main JPX decoder interface. Handles the complete decoding process from 
/// parsed header and codestream to decoded image data.
/// </summary>
internal interface IJpxDecoder
{
    /// <summary>
    /// Decodes a JPX image from header and codestream data.
    /// </summary>
    /// <param name="header">Parsed JPX header containing image metadata.</param>
    /// <param name="codestream">Raw codestream data starting from SOC marker.</param>
    /// <returns>Row provider for streaming decoded image data.</returns>
    IJpxRowProvider Decode(JpxHeader header, ReadOnlySpan<byte> codestream);
}

/// <summary>
/// Interface for providing decoded image data row by row.
/// This bridges JPX tile-based decoding with PDF's row-based processing.
/// </summary>
internal interface IJpxRowProvider : IDisposable
{
    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the number of components per pixel.
    /// </summary>
    int ComponentCount { get; }

    /// <summary>
    /// Gets the current row index (0-based).
    /// </summary>
    int CurrentRow { get; }

    /// <summary>
    /// Attempts to read the next row of image data.
    /// </summary>
    /// <param name="rowBuffer">Buffer to receive the row data. Must be at least Width * ComponentCount bytes.</param>
    /// <returns>True if a row was successfully read, false if at end of image.</returns>
    bool TryGetNextRow(Span<byte> rowBuffer);
}

/// <summary>
/// Interface for decoding individual JPX tiles.
/// </summary>
internal interface IJpxTileDecoder
{
    /// <summary>
    /// Decodes a single tile from tile-part data.
    /// </summary>
    /// <param name="tileHeader">Tile-specific header information.</param>
    /// <param name="tileData">Encoded tile data.</param>
    /// <returns>Decoded tile with component data.</returns>
    JpxTile DecodeTile(JpxTileHeader tileHeader, ReadOnlySpan<byte> tileData);
}
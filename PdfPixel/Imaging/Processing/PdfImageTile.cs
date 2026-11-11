using PdfPixel.Geometry;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// A view over the decoded samples of one tile, at their source bit depth, row by row.
/// </summary>
public readonly struct PdfImageTile
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _rowStride;

    /// <summary>
    /// Initializes a view over <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The samples, the first byte holding the first row of the tile.</param>
    /// <param name="rowStride">The number of bytes from one row to the next.</param>
    /// <param name="bounds">The samples of the grid the tile covers.</param>
    /// <param name="columnOrigin">The sample column the first byte of each row starts at.</param>
    /// <param name="bitsPerComponent">The bit depth of a component.</param>
    /// <param name="componentCount">The number of components of a sample.</param>
    public PdfImageTile(
        in ReadOnlyMemory<byte> data,
        int rowStride,
        in PdfIntegerRectangle bounds,
        int columnOrigin,
        int bitsPerComponent,
        int componentCount)
    {
        _data = data;
        _rowStride = rowStride;
        Bounds = bounds;
        ColumnOrigin = columnOrigin;
        BitsPerComponent = bitsPerComponent;
        ComponentCount = componentCount;
    }

    /// <summary>
    /// Gets the samples of the grid the tile covers.
    /// </summary>
    public PdfIntegerRectangle Bounds { get; }

    /// <summary>
    /// Gets the sample column the first byte of each row starts at.
    /// </summary>
    public int ColumnOrigin { get; }

    /// <summary>
    /// Gets the bit depth of a component.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Gets the number of components of a sample.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Returns the bytes of the grid's sample row <paramref name="row"/>, from <see cref="ColumnOrigin"/>.
    /// </summary>
    public ReadOnlySpan<byte> GetRow(int row) => _data.Span.Slice((row - Bounds.Top) * _rowStride, _rowStride);

    /// <summary>
    /// Returns the code of component <paramref name="component"/> at the grid's sample <paramref name="column"/> of
    /// <paramref name="row"/>.
    /// </summary>
    public int ReadCode(in ReadOnlySpan<byte> row, int column, int component)
    {
        int bitOffset = (((column - ColumnOrigin) * ComponentCount) + component) * BitsPerComponent;

        switch (BitsPerComponent)
        {
            case 8:
            {
                return row[bitOffset >> 3];
            }
            case 16:
            {
                int byteOffset = bitOffset >> 3;

                return (row[byteOffset] << 8) | row[byteOffset + 1];
            }
            default:
            {
                int code = 0;

                for (int bit = 0; bit < BitsPerComponent; bit++)
                {
                    int position = bitOffset + bit;
                    code = (code << 1) | ((row[position >> 3] >> (7 - (position & 7))) & 1);
                }

                return code;
            }
        }
    }
}

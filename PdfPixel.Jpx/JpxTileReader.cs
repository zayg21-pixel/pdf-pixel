using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Jpx;

/// <summary>
/// Reads the tiles of a JPEG 2000 codestream into buffers, the selected components of a sample packed together at
/// <see cref="BitsPerComponent"/>, in the order of the component selection.
/// </summary>
public sealed class JpxTileReader
{
    private readonly JpxTileProvider _tileProvider;
    private readonly JpxTile _tile;
    private readonly int[] _components;
    private readonly int[] _sampleBias;
    private readonly int[] _sampleShift;
    private readonly int[][] _componentPlanes;

    /// <summary>
    /// Initializes a reader of the codestream that follows <paramref name="header"/>.
    /// </summary>
    /// <param name="header">The parsed JPX header.</param>
    /// <param name="codestream">The codestream data, from the first SOT marker.</param>
    /// <param name="decodingParameters">The resolution and the components to reconstruct.</param>
    public JpxTileReader(JpxHeader header, in ReadOnlyMemory<byte> codestream, in JpxDecodingParameters decodingParameters = default)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        JpxDecodingParameters parameters = (decodingParameters.DescaleFactor >= 1) ? decodingParameters : JpxDecodingParameters.Default;

        _tileProvider = new JpxTileProvider(header, codestream, parameters);
        _tile = _tileProvider.CreateTile();

        // TODO: [HIGH] Account for the reference grid origin. The image spans Xsiz - XOsiz by
        // Ysiz - YOsiz, but these take Xsiz and Ysiz whole, so an image whose origin is away from
        // zero comes out too large by the origin and its samples land at the wrong offset. The
        // tile grid needs the same treatment, since a tile starts at XTOsiz + index * XTsiz.
        // Covered by the failing baboon-offset-image and baboon-offset-tile corpus files.
        Width = parameters.ReduceDimension((int)header.Width);
        Height = parameters.ReduceDimension((int)header.Height);
        TileWidth = parameters.ReduceDimension((int)header.TileWidth);
        TileHeight = parameters.ReduceDimension((int)header.TileHeight);

        IReadOnlyList<int> componentSelection = _tileProvider.ComponentSelection;
        int componentCount = componentSelection.Count;
        int precisionBits = 0;

        _components = new int[componentCount];

        for (int index = 0; index < componentCount; index++)
        {
            _components[index] = componentSelection[index];
            precisionBits = Math.Max(precisionBits, header.Components[_components[index]].PrecisionBits);
        }

        BitsPerComponent = Math.Min(16, Math.Max(1, precisionBits));
        _sampleBias = new int[componentCount];
        _sampleShift = new int[componentCount];
        _componentPlanes = new int[componentCount][];

        for (int index = 0; index < componentCount; index++)
        {
            JpxComponent componentInfo = header.Components[_components[index]];

            _sampleBias[index] = (componentInfo.IsSigned) ? 1 << (componentInfo.PrecisionBits - 1) : 0;
            _sampleShift[index] = BitsPerComponent - componentInfo.PrecisionBits;
        }
    }

    /// <summary>
    /// Gets the image width in samples, at the decoded resolution.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the image height in samples, at the decoded resolution.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the width of a tile in samples, at the decoded resolution.
    /// </summary>
    public int TileWidth { get; }

    /// <summary>
    /// Gets the height of a tile in samples, at the decoded resolution.
    /// </summary>
    public int TileHeight { get; }

    /// <summary>
    /// Gets the number of tile columns.
    /// </summary>
    public int TilesHorizontal => _tileProvider.TilesHorizontal;

    /// <summary>
    /// Gets the number of tile rows.
    /// </summary>
    public int TilesVertical => _tileProvider.TilesVertical;

    /// <summary>
    /// Gets the number of tiles.
    /// </summary>
    public int TotalTiles => _tileProvider.TotalTiles;

    /// <summary>
    /// Gets the components read, by index in the codestream, in the order they are packed.
    /// </summary>
    public IReadOnlyList<int> ComponentSelection => _components;

    /// <summary>
    /// Gets the number of components packed per sample.
    /// </summary>
    public int ComponentCount => _components.Length;

    /// <summary>
    /// Gets the bit depth components are packed at: the largest precision of the selected components, at most 16.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Returns the samples the tile at <paramref name="tileIndex"/> covers, at the decoded resolution.
    /// </summary>
    public JpxRectangle GetTileBounds(int tileIndex)
    {
        int x = (tileIndex % TilesHorizontal) * TileWidth;
        int y = (tileIndex / TilesHorizontal) * TileHeight;

        return new JpxRectangle(x, y, Math.Min(TileWidth, Width - x), Math.Min(TileHeight, Height - y));
    }

    /// <summary>
    /// Returns the number of bytes <paramref name="width"/> packed samples take.
    /// </summary>
    public int GetRowBytes(int width) => ((width * ComponentCount * BitsPerComponent) + 7) / 8;

    /// <summary>
    /// Decodes the tile at <paramref name="tileIndex"/> and writes its rows to <paramref name="destination"/>, one
    /// every <paramref name="rowStride"/> bytes, each from sample column <paramref name="sampleOffset"/>.
    /// </summary>
    /// <param name="tileIndex">Index of the tile, in row-major order.</param>
    /// <param name="destination">Receives the tile's rows.</param>
    /// <param name="rowStride">Bytes from one row of <paramref name="destination"/> to the next.</param>
    /// <param name="sampleOffset">The sample column of each row the tile's first sample is written at.</param>
    /// <param name="observer">Observer notified on progress.</param>
    public void ReadTile(int tileIndex, in Span<byte> destination, int rowStride, int sampleOffset, IJpxExecutionObserver? observer = default)
    {
        JpxRectangle bounds = GetTileBounds(tileIndex);

        _tileProvider.DecodeTile(tileIndex, _tile, observer);

        int width = Math.Min(bounds.Width, _tile.Width);
        int height = Math.Min(bounds.Height, _tile.Height);

        if (width <= 0)
        {
            return;
        }

        for (int index = 0; index < _components.Length; index++)
        {
            _componentPlanes[index] = _tile.ComponentData[_components[index]];
        }

        AdjustSamples(_tile.Width * height);

        for (int row = 0; row < height; row++)
        {
            Span<byte> rowBytes = destination.Slice(row * rowStride, rowStride);
            int sampleRowOffset = row * _tile.Width;

            switch (BitsPerComponent)
            {
                case 8:
                {
                    WriteByteRow(rowBytes, sampleRowOffset, sampleOffset, width);
                    break;
                }
                case 16:
                {
                    WriteWordRow(rowBytes, sampleRowOffset, sampleOffset, width);
                    break;
                }
                default:
                {
                    WritePackedRow(rowBytes, sampleRowOffset, sampleOffset, width);
                    break;
                }
            }

            observer?.Notify();
        }
    }

    /// <summary>
    /// Brings the first <paramref name="sampleCount"/> samples of every selected component to unsigned codes at
    /// <see cref="BitsPerComponent"/>, in place: a signed component is biased by half its range, and a component
    /// whose precision differs is shifted by the difference. A component selected twice is adjusted once.
    /// </summary>
    private void AdjustSamples(int sampleCount)
    {
        for (int index = 0; index < _components.Length; index++)
        {
            int bias = _sampleBias[index];
            int shift = _sampleShift[index];

            if ((bias == 0 && shift == 0) || IsSelectedEarlier(index))
            {
                continue;
            }

            Span<int> plane = _componentPlanes[index].AsSpan(0, sampleCount);

            if (shift >= 0)
            {
                for (int sample = 0; sample < plane.Length; sample++)
                {
                    plane[sample] = (int)((uint)(plane[sample] + bias) << shift);
                }
            }
            else
            {
                int rightShift = -shift;

                for (int sample = 0; sample < plane.Length; sample++)
                {
                    plane[sample] = (int)((uint)(plane[sample] + bias) >> rightShift);
                }
            }
        }
    }

    /// <summary>
    /// Whether the component at <paramref name="index"/> of the selection is also selected before it.
    /// </summary>
    private bool IsSelectedEarlier(int index)
    {
        for (int earlier = 0; earlier < index; earlier++)
        {
            if (_components[earlier] == _components[index])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes <paramref name="width"/> samples of a tile row at 8 bits per component, from sample column
    /// <paramref name="sampleOffset"/> of <paramref name="row"/>.
    /// </summary>
    private void WriteByteRow(in Span<byte> row, int sampleRowOffset, int sampleOffset, int width)
    {
        int componentCount = _components.Length;
        Span<byte> samples = row.Slice(sampleOffset * componentCount, width * componentCount);
        ref byte firstSample = ref MemoryMarshal.GetReference(samples);

        for (int index = 0; index < componentCount; index++)
        {
            ReadOnlySpan<int> plane = _componentPlanes[index].AsSpan(sampleRowOffset, width);
            ref int firstValue = ref MemoryMarshal.GetReference(plane);

            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref firstSample, (column * componentCount) + index) = (byte)Unsafe.Add(ref firstValue, column);
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="width"/> samples of a tile row at 16 bits per component, most significant byte first,
    /// from sample column <paramref name="sampleOffset"/> of <paramref name="row"/>.
    /// </summary>
    private void WriteWordRow(in Span<byte> row, int sampleRowOffset, int sampleOffset, int width)
    {
        int componentCount = _components.Length;
        Span<byte> samples = row.Slice(sampleOffset * componentCount * 2, width * componentCount * 2);
        ref byte firstSample = ref MemoryMarshal.GetReference(samples);

        for (int index = 0; index < componentCount; index++)
        {
            ReadOnlySpan<int> plane = _componentPlanes[index].AsSpan(sampleRowOffset, width);
            ref int firstValue = ref MemoryMarshal.GetReference(plane);

            for (int column = 0; column < width; column++)
            {
                int value = Unsafe.Add(ref firstValue, column);
                int offset = ((column * componentCount) + index) * 2;

                Unsafe.Add(ref firstSample, offset) = (byte)(value >> 8);
                Unsafe.Add(ref firstSample, offset + 1) = (byte)value;
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="width"/> samples of a tile row packed at <see cref="BitsPerComponent"/>, most significant
    /// bit first, from sample column <paramref name="sampleOffset"/> of <paramref name="row"/>. The bits of the first
    /// and last bytes outside the samples are kept.
    /// </summary>
    private void WritePackedRow(in Span<byte> row, int sampleRowOffset, int sampleOffset, int width)
    {
        int componentCount = _components.Length;
        int bitsPerComponent = BitsPerComponent;
        uint codeMask = (1u << bitsPerComponent) - 1u;
        int bitPosition = sampleOffset * componentCount * bitsPerComponent;
        int byteIndex = bitPosition >> 3;
        int pendingBits = bitPosition & 7;
        uint pending = (uint)row[byteIndex] >> (8 - pendingBits);

        for (int column = 0; column < width; column++)
        {
            int sampleIndex = sampleRowOffset + column;

            for (int index = 0; index < componentCount; index++)
            {
                pending = (pending << bitsPerComponent) | ((uint)_componentPlanes[index][sampleIndex] & codeMask);
                pendingBits += bitsPerComponent;

                while (pendingBits >= 8)
                {
                    pendingBits -= 8;
                    row[byteIndex] = (byte)(pending >> pendingBits);
                    byteIndex++;
                }
            }
        }

        if (pendingBits > 0)
        {
            int keptMask = 0xFF >> pendingBits;

            row[byteIndex] = (byte)(((pending << (8 - pendingBits)) & 0xFF) | (uint)(row[byteIndex] & keptMask));
        }
    }
}

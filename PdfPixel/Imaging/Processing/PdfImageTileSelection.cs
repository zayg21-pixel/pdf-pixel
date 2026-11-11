using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// The tiles of one set of samples a decoder reads while an image is drawn, found in or rented from an
/// <see cref="IPdfImageCache"/>, each put into it once decoded.
/// </summary>
internal sealed class PdfImageTileSelection
{
    private ReadOnlyMemory<byte>?[] _tiles = [];
    private byte[]?[] _writtenTiles = [];
    private PdfImageSampleInfo? _samples;
    private int _plane;
    private IPdfImageCache? _cache;
    private PdfImage? _image;

    /// <summary>
    /// Selects tiles of <paramref name="samples"/> of <paramref name="plane"/>, dropping every tile of other samples.
    /// </summary>
    public void SetSamples(PdfImageSampleInfo samples, int plane)
    {
        if (!ReferenceEquals(_samples, samples))
        {
            _samples = samples;
            _plane = plane;
            _tiles = new ReadOnlyMemory<byte>?[samples.Grid.TotalTiles];
            _writtenTiles = new byte[]?[samples.Grid.TotalTiles];
        }
    }

    /// <summary>
    /// Finds and decodes the tiles of <paramref name="image"/> in <paramref name="cache"/> from now on.
    /// </summary>
    public void SetCache(IPdfImageCache cache, PdfImage image)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>
    /// Returns whether the tile at <paramref name="tileIndex"/> is decoded, looking for it in the cache.
    /// </summary>
    public bool Contains(int tileIndex)
    {
        if (_tiles[tileIndex] != null)
        {
            return true;
        }

        (IPdfImageCache cache, PdfImage image) = GetCache();
        ReadOnlyMemory<byte>? cached = cache.Find(image, GetKey(tileIndex));

        if (cached == null)
        {
            return false;
        }

        _tiles[tileIndex] = cached;

        return true;
    }

    /// <summary>
    /// Returns the decoded tile at <paramref name="tileIndex"/>, or <see langword="null"/> when it is not decoded.
    /// </summary>
    public PdfImageTile? GetTile(int tileIndex)
    {
        ReadOnlyMemory<byte>? data = _tiles[tileIndex];

        if (_samples == null || data == null)
        {
            return null;
        }

        PdfIntegerRectangle bounds = _samples.Grid.GetTilePosition(tileIndex);

        return new PdfImageTile(data.Value, GetRowStride(_samples, bounds.Width), bounds, bounds.Left, _samples.BitsPerComponent, _samples.ComponentCount);
    }

    /// <summary>
    /// Returns the whole tile at <paramref name="tileIndex"/> to decode into, rented from the cache.
    /// </summary>
    public Span<byte> GetTileForWriting(int tileIndex)
    {
        if (_samples == null)
        {
            throw new InvalidOperationException("No samples are set.");
        }

        return RentTile(tileIndex, _samples);
    }

    /// <summary>
    /// Returns row <paramref name="row"/> of the tile at <paramref name="tileIndex"/> to decode into, rented from the
    /// cache.
    /// </summary>
    public Span<byte> GetRowForWriting(int tileIndex, int row)
    {
        if (_samples == null)
        {
            throw new InvalidOperationException("No samples are set.");
        }

        PdfIntegerRectangle bounds = _samples.Grid.GetTilePosition(tileIndex);
        int rowStride = GetRowStride(_samples, bounds.Width);
        byte[] data = RentTile(tileIndex, _samples);

        return data.AsSpan((row - bounds.Top) * rowStride, rowStride);
    }

    /// <summary>
    /// Marks the tile at <paramref name="tileIndex"/>, written in full, as decoded and puts it into the cache.
    /// </summary>
    public void Complete(int tileIndex)
    {
        byte[]? data = _writtenTiles[tileIndex];

        if (data == null)
        {
            throw new InvalidOperationException("The tile was not written.");
        }

        (IPdfImageCache cache, PdfImage image) = GetCache();

        cache.Put(image, GetKey(tileIndex), data);
        _tiles[tileIndex] = data;
        _writtenTiles[tileIndex] = null;
    }

    /// <summary>
    /// Drops the tiles outside <paramref name="tileRange"/>.
    /// </summary>
    public void EvictOutside(in PdfIntegerRectangle tileRange)
    {
        if (_samples == null)
        {
            return;
        }

        for (int tileIndex = 0; tileIndex < _tiles.Length; tileIndex++)
        {
            if (!_samples.Grid.IsTileInRange(tileIndex, tileRange))
            {
                _tiles[tileIndex] = null;
                _writtenTiles[tileIndex] = null;
            }
        }
    }

    /// <summary>
    /// Drops the tile at <paramref name="tileIndex"/>, decoded or not.
    /// </summary>
    public void Remove(int tileIndex)
    {
        _tiles[tileIndex] = null;
        _writtenTiles[tileIndex] = null;
    }

    /// <summary>
    /// Drops every tile and the cache.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_tiles, 0, _tiles.Length);
        Array.Clear(_writtenTiles, 0, _writtenTiles.Length);
        _cache = null;
        _image = null;
    }

    /// <summary>
    /// Returns the number of bytes of a row of <paramref name="width"/> samples.
    /// </summary>
    public static int GetRowStride(PdfImageSampleInfo samples, int width) => ((width * samples.ComponentCount * samples.BitsPerComponent) + 7) / 8;

    private byte[] RentTile(int tileIndex, PdfImageSampleInfo samples)
    {
        byte[]? data = _writtenTiles[tileIndex];

        if (data == null)
        {
            PdfIntegerRectangle bounds = samples.Grid.GetTilePosition(tileIndex);
            (IPdfImageCache cache, PdfImage image) = GetCache();

            int length = GetRowStride(samples, bounds.Width) * bounds.Height;

            // Rows a truncated image never reaches stay zero.
            data = cache.Rent(image, length);
            data.AsSpan(0, length).Clear();
            _writtenTiles[tileIndex] = data;
        }

        return data;
    }

    private PdfImageCacheKey GetKey(int tileIndex)
    {
        if (_samples == null)
        {
            throw new InvalidOperationException("No samples are set.");
        }

        return new PdfImageCacheKey(_plane, _samples.DescaleFactor, tileIndex);
    }

    private (IPdfImageCache Cache, PdfImage Image) GetCache()
    {
        if (_cache == null || _image == null)
        {
            throw new InvalidOperationException("No cache is set.");
        }

        return (_cache, _image);
    }
}

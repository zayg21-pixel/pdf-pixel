using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// The pixels of page tiles of one tile size, square opaque DeviceRGB samples, kept for reuse while the rented and free
/// tiles together stay within the tile capacity of the last content request.
/// </summary>
internal sealed class PdfPageTilePool
{
    private const int TileBitsPerComponent = 8;

    private static readonly PdfImageChannel[] TileChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2)];

    private readonly object _locker = new();
    private readonly Stack<PdfImageSamples> _freeSamples = [];
    private int _rentedCount;
    private int _capacity;

    /// <summary>
    /// Initializes a pool of tiles of <paramref name="tileSize"/> by <paramref name="tileSize"/> pixels.
    /// </summary>
    public PdfPageTilePool(int tileSize)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        TileSize = tileSize;
    }

    /// <summary>
    /// Gets the edge length of a tile in device pixels.
    /// </summary>
    public int TileSize { get; }

    /// <summary>
    /// Returns the samples of a tile, a free one when the pool keeps one, otherwise new ones; their pixels are
    /// undefined.
    /// </summary>
    public PdfImageSamples Rent()
    {
        lock (_locker)
        {
            _rentedCount++;

            if (_freeSamples.Count > 0)
            {
                return _freeSamples.Pop();
            }
        }

        return new PdfImageSamples(TileSize, TileSize, PdfImageSampleFormat.Integer, TileBitsPerComponent, TileChannels, isPremultiplied: false);
    }

    /// <summary>
    /// Takes back <paramref name="samples"/> rented with <see cref="Rent"/>, keeping them while the rented and free
    /// tiles together stay within the capacity.
    /// </summary>
    public void Return(PdfImageSamples samples)
    {
        if (samples == null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        lock (_locker)
        {
            _rentedCount--;

            if (_rentedCount + _freeSamples.Count < _capacity)
            {
                _freeSamples.Push(samples);
            }
        }
    }

    /// <summary>
    /// Sets the number of tiles the pool keeps, rented and free together, dropping the free tiles beyond it.
    /// </summary>
    public void UpdateCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        lock (_locker)
        {
            _capacity = capacity;

            while (_freeSamples.Count > 0 && _rentedCount + _freeSamples.Count > _capacity)
            {
                _freeSamples.Pop();
            }
        }
    }
}

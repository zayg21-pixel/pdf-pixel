using PdfPixel.Imaging;
using PdfPixel.Imaging.Decoding;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap;

/// <summary>
/// The image cache Snap surfaces decode images into: the data put is kept after a draw while it fits its capacity, the
/// images drawn longest ago dropped first, and the memory rented is reused while it fits its own capacity.
/// </summary>
public sealed class PdfSnapImageCache : IPdfImageCache
{
    private const long BytesPerMegabyte = 1024 * 1024;
    private const int DefaultTileSize = 256;
    private const int DefaultPutCapacityMegabytes = 32;
    private const int DefaultRentCapacityMegabytes = 32;

    private readonly object _locker = new();
    private readonly Dictionary<PdfImage, ImageRecord> _records = new(ImageComparer.Instance);
    private readonly Dictionary<int, Stack<byte[]>> _freeBlocks = [];
    private readonly long _putCapacity;
    private readonly long _rentCapacity;
    private long _keptBytes;
    private long _freeBytes;
    private long _releaseCount;

    /// <summary>
    /// Initializes a cache of tiles of 256 samples, keeping up to 32 megabytes of decoded data and reusing up to 32
    /// megabytes of decoding memory.
    /// </summary>
    public PdfSnapImageCache()
        : this(DefaultTileSize, DefaultPutCapacityMegabytes, DefaultRentCapacityMegabytes)
    {
    }

    /// <summary>
    /// Initializes a cache of tiles of <paramref name="tileSize"/>, keeping up to
    /// <paramref name="putCapacityMegabytes"/> of decoded data and reusing up to <paramref name="rentCapacityMegabytes"/>
    /// of decoding memory.
    /// </summary>
    /// <param name="tileSize">The edge length, in samples, of the tiles decoders split images into.</param>
    /// <param name="putCapacityMegabytes">The decoded data kept after a draw, in megabytes.</param>
    /// <param name="rentCapacityMegabytes">The decoding memory kept for reuse, in megabytes.</param>
    public PdfSnapImageCache(int tileSize, int putCapacityMegabytes, int rentCapacityMegabytes)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        if (putCapacityMegabytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(putCapacityMegabytes));
        }

        if (rentCapacityMegabytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rentCapacityMegabytes));
        }

        TileSize = tileSize;
        _putCapacity = putCapacityMegabytes * BytesPerMegabyte;
        _rentCapacity = rentCapacityMegabytes * BytesPerMegabyte;
    }

    /// <inheritdoc />
    public int TileSize { get; }

    /// <inheritdoc />
    public byte[] Rent(PdfImage image, int length)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        lock (_locker)
        {
            ImageRecord record = GetRecord(image);
            byte[]? block = null;

            if (_freeBlocks.TryGetValue(length, out Stack<byte[]>? blocks) && blocks.Count > 0)
            {
                block = blocks.Pop();
                _freeBytes -= length;
            }

            if (block == null)
            {
                block = new byte[length];
            }

            record.RentedBlocks.Add(block);

            return block;
        }
    }

    /// <inheritdoc />
    public void Put(PdfImage image, in PdfImageCacheKey key, in ReadOnlyMemory<byte> data)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        lock (_locker)
        {
            ImageRecord record = GetRecord(image);

            if (record.Entries.TryGetValue(key, out ReadOnlyMemory<byte> previous))
            {
                _keptBytes -= GetKeptLength(previous);
            }

            record.Entries[key] = data;
            _keptBytes += GetKeptLength(data);

            // A rented block put as data belongs to its entry from now on.
            if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment) && segment.Array != null)
            {
                record.RentedBlocks.Remove(segment.Array);
            }
        }
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte>? Find(PdfImage image, in PdfImageCacheKey key)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        lock (_locker)
        {
            if (!_records.TryGetValue(image, out ImageRecord? record) || !record.Entries.TryGetValue(key, out ReadOnlyMemory<byte> data))
            {
                return null;
            }

            record.IsInUse = true;

            return data;
        }
    }

    /// <inheritdoc />
    public void Release(PdfImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        lock (_locker)
        {
            if (!_records.TryGetValue(image, out ImageRecord? record))
            {
                return;
            }

            foreach (byte[] block in record.RentedBlocks)
            {
                ReturnBlock(block);
            }

            record.RentedBlocks.Clear();
            record.IsInUse = false;
            _releaseCount++;
            record.ReleaseOrder = _releaseCount;

            if (record.Entries.Count == 0)
            {
                _records.Remove(image);
            }

            Trim();
        }
    }

    private ImageRecord GetRecord(PdfImage image)
    {
        if (!_records.TryGetValue(image, out ImageRecord? record))
        {
            record = new ImageRecord();
            _records.Add(image, record);
        }

        record.IsInUse = true;

        return record;
    }

    /// <summary>
    /// Keeps <paramref name="block"/> for reuse while the free blocks fit the rent capacity, otherwise drops it.
    /// </summary>
    private void ReturnBlock(byte[] block)
    {
        if (_freeBytes + block.Length > _rentCapacity)
        {
            return;
        }

        if (!_freeBlocks.TryGetValue(block.Length, out Stack<byte[]>? blocks))
        {
            blocks = new Stack<byte[]>();
            _freeBlocks.Add(block.Length, blocks);
        }

        blocks.Push(block);
        _freeBytes += block.Length;
    }

    /// <summary>
    /// Drops the data of the images released longest ago until the kept data fits the put capacity; images being drawn
    /// are never dropped.
    /// </summary>
    private void Trim()
    {
        while (_keptBytes > _putCapacity)
        {
            PdfImage? oldestImage = null;
            ImageRecord? oldestRecord = null;

            foreach (KeyValuePair<PdfImage, ImageRecord> pair in _records)
            {
                if (!pair.Value.IsInUse && (oldestRecord == null || pair.Value.ReleaseOrder < oldestRecord.ReleaseOrder))
                {
                    oldestImage = pair.Key;
                    oldestRecord = pair.Value;
                }
            }

            if (oldestImage == null || oldestRecord == null)
            {
                return;
            }

            foreach (ReadOnlyMemory<byte> data in oldestRecord.Entries.Values)
            {
                _keptBytes -= GetKeptLength(data);
            }

            _records.Remove(oldestImage);
        }
    }

    /// <summary>
    /// Returns the bytes keeping <paramref name="data"/> holds alive: the whole array it is a part of.
    /// </summary>
    private static long GetKeptLength(in ReadOnlyMemory<byte> data)
    {
        if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment) && segment.Array != null)
        {
            return segment.Array.Length;
        }

        return data.Length;
    }

    /// <summary>
    /// The data kept for one image and the memory rented for it while it is drawn.
    /// </summary>
    private sealed class ImageRecord
    {
        public Dictionary<PdfImageCacheKey, ReadOnlyMemory<byte>> Entries { get; } = [];

        public List<byte[]> RentedBlocks { get; } = [];

        public bool IsInUse { get; set; }

        public long ReleaseOrder { get; set; }
    }

    /// <summary>
    /// Compares images by reference: the document keeps one instance per image.
    /// </summary>
    private sealed class ImageComparer : IEqualityComparer<PdfImage>
    {
        public static readonly ImageComparer Instance = new();

        public bool Equals(PdfImage? x, PdfImage? y) => ReferenceEquals(x, y);

        public int GetHashCode(PdfImage obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

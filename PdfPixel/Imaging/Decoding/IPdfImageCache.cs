using System;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// The memory image decoders decode into and the decoded data they keep, per image: memory is rented and data put
/// while an image is drawn, and all of it is returned when the draw ends; the cache then keeps or drops it.
/// </summary>
public interface IPdfImageCache
{
    /// <summary>
    /// Gets the edge length, in samples, of the tiles decoders split images into.
    /// </summary>
    int TileSize { get; }

    /// <summary>
    /// Returns memory of <paramref name="length"/> bytes to decode <paramref name="image"/> into, held until
    /// <see cref="Release"/>; its bytes are undefined.
    /// </summary>
    /// <param name="image">The image the memory is decoded for.</param>
    /// <param name="length">The number of bytes.</param>
    byte[] Rent(PdfImage image, int length);

    /// <summary>
    /// Keeps <paramref name="data"/>, decoded from <paramref name="image"/>, under <paramref name="key"/>, until the
    /// cache drops it after <see cref="Release"/>.
    /// </summary>
    /// <param name="image">The image the data is decoded from.</param>
    /// <param name="key">Which data of the image it is.</param>
    /// <param name="data">The decoded data, rented from this cache or created by the decoder.</param>
    void Put(PdfImage image, in PdfImageCacheKey key, in ReadOnlyMemory<byte> data);

    /// <summary>
    /// Returns the data kept under <paramref name="key"/> for <paramref name="image"/>, held until
    /// <see cref="Release"/>, or <see langword="null"/> when the cache no longer has it.
    /// </summary>
    /// <param name="image">The image the data was decoded from.</param>
    /// <param name="key">Which data of the image it is.</param>
    ReadOnlyMemory<byte>? Find(PdfImage image, in PdfImageCacheKey key);

    /// <summary>
    /// Returns everything rented, put or found for <paramref name="image"/>; the cache keeps the data put, within its
    /// capacity, and reuses or drops the rest.
    /// </summary>
    /// <param name="image">The image whose draw ended.</param>
    void Release(PdfImage image);
}

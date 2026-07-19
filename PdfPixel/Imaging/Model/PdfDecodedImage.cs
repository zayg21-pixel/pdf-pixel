using System;
using System.Buffers;

namespace PdfPixel.Imaging.Model;

/// <summary>
/// Fully decoded pixel data for an image or image tile. Rents its backing buffer from the shared
/// array pool and returns it on <see cref="Dispose"/>.
/// </summary>
public sealed class PdfDecodedImage : IDisposable
{
    private readonly int _rowBytes;
    private byte[]? _buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDecodedImage"/> class, renting a buffer sized
    /// for the given dimensions and color format.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="colorFormat">Layout of each pixel in the buffer.</param>
    public PdfDecodedImage(int width, int height, PdfImageColorFormat colorFormat)
    {
        Width = width;
        Height = height;
        ColorFormat = colorFormat;
        _rowBytes = width * ((colorFormat == PdfImageColorFormat.Gray) ? 1 : 4);
        _buffer = ArrayPool<byte>.Shared.Rent(_rowBytes * height);
    }

    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the layout of each pixel in the buffer.
    /// </summary>
    public PdfImageColorFormat ColorFormat { get; }

    /// <summary>
    /// Gets a writable view over the full packed pixel buffer, row-major with no padding between rows.
    /// </summary>
    internal Span<byte> GetRawBuffer() => GetBuffer().AsSpan(0, _rowBytes * Height);

    /// <summary>
    /// Gets a writable view over a single row of the pixel buffer.
    /// </summary>
    internal Span<byte> GetRow(int row) => GetBuffer().AsSpan(row * _rowBytes, _rowBytes);

    private byte[] GetBuffer() => _buffer ?? throw new ObjectDisposedException(nameof(PdfDecodedImage));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}

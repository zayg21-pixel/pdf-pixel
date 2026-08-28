using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Model;

/// <summary>
/// Fully decoded pixel data for an image or image tile.
/// </summary>
public sealed class PdfDecodedImage
{
    private readonly int _rowBytes;
    private byte[] _buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDecodedImage"/> class, allocating a buffer sized
    /// for the given dimensions and color format.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="colorFormat">Layout of each pixel in the buffer.</param>
    /// <param name="alphaType">How the buffer's alpha relates to its color samples.</param>
    public PdfDecodedImage(int width, int height, PdfImageColorFormat colorFormat, PdfImageAlphaType alphaType)
    {
        Width = width;
        Height = height;
        ColorFormat = colorFormat;
        AlphaType = alphaType;
        _rowBytes = width * ((colorFormat == PdfImageColorFormat.Rgba) ? 4 : 1);

        int bufferLength = _rowBytes * height;
#if NET5_0_OR_GREATER
        _buffer = GC.AllocateUninitializedArray<byte>(bufferLength);
#else
        _buffer = new byte[bufferLength];
#endif
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
    /// Gets how the buffer's alpha relates to its color samples.
    /// </summary>
    public PdfImageAlphaType AlphaType { get; }

    /// <summary>
    /// Gets the number of bytes occupied by a single row of the pixel buffer.
    /// </summary>
    internal int RowBytes => _rowBytes;

    /// <summary>
    /// Gets a writable view over the full packed pixel buffer, row-major with no padding between rows.
    /// </summary>
    internal Span<byte> GetRawBuffer() => GetBuffer().AsSpan(0, _rowBytes * Height);

    /// <summary>
    /// Gets a writable view over a single row of the pixel buffer.
    /// </summary>
    internal Span<byte> GetRow(int row) => GetBuffer().AsSpan(row * _rowBytes, _rowBytes);

    /// <summary>
    /// Pins the pixel buffer and returns the handle the caller must free.
    /// </summary>
    internal GCHandle PinBuffer() => GCHandle.Alloc(GetBuffer(), GCHandleType.Pinned);

    private byte[] GetBuffer() => _buffer ?? throw new ObjectDisposedException(nameof(PdfDecodedImage));
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// The samples of an image or image tile, row-major from the top row.
/// </summary>
public sealed class PdfImageSamples
{
    private const int FloatBitsPerComponent = 32;
    private const int MaximumIntegerBitsPerComponent = 16;

    private readonly PdfImageChannel[] _channels;
    private readonly byte[] _buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageSamples"/> class, allocating a buffer sized
    /// for the given dimensions and layout.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="format">How each sample is stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 1 to 16 for integers, 32 for floats.</param>
    /// <param name="channels">What each stored channel of a pixel holds, in storage order.</param>
    public PdfImageSamples(int width, int height, PdfImageSampleFormat format, int bitsPerComponent, PdfImageChannel[] channels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        if (format == PdfImageSampleFormat.Float && bitsPerComponent != FloatBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Float samples must be 32 bits per component.");
        }

        if (format == PdfImageSampleFormat.Integer && (bitsPerComponent < 1 || bitsPerComponent > MaximumIntegerBitsPerComponent))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Integer samples must be 1 to 16 bits per component.");
        }

        if (channels == null || channels.Length == 0)
        {
            throw new ArgumentException("At least one channel is required.", nameof(channels));
        }

        Width = width;
        Height = height;
        Format = format;
        BitsPerComponent = bitsPerComponent;
        _channels = channels;
        RowBytes = ((width * channels.Length * bitsPerComponent) + 7) / 8;

        int bufferLength = RowBytes * height;
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
    /// Gets how each sample is stored.
    /// </summary>
    public PdfImageSampleFormat Format { get; }

    /// <summary>
    /// Gets the bit depth of each sample.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Gets what each stored channel of a pixel holds, in storage order.
    /// </summary>
    public IReadOnlyList<PdfImageChannel> Channels => _channels;

    /// <summary>
    /// Gets the number of bytes occupied by a single row.
    /// </summary>
    public int RowBytes { get; }

    /// <summary>
    /// Gets a read-only view over all rows, with no padding between rows.
    /// </summary>
    public ReadOnlySpan<byte> Pixels => _buffer;

    /// <summary>
    /// Gets a writable view over the row at <paramref name="row"/>.
    /// </summary>
    public Span<byte> GetRow(int row) => _buffer.AsSpan(row * RowBytes, RowBytes);

    /// <summary>
    /// Pins the sample buffer and returns the handle the caller must free.
    /// </summary>
    internal GCHandle PinBuffer() => GCHandle.Alloc(_buffer, GCHandleType.Pinned);
}

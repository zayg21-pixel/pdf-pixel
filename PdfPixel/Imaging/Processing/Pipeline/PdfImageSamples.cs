using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// The samples of an image or image tile, row-major from the top row.
/// </summary>
public sealed class PdfImageSamples : IPdfImageRowTarget
{
    private const int FloatBitsPerComponent = 32;
    private const int MaximumIntegerBitsPerComponent = 16;

    private readonly PdfImageChannel[] _channels;
    private readonly byte[] _buffer;
    private readonly int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageSamples"/> class, allocating a buffer sized
    /// for the given dimensions and layout.
    /// </summary>
    /// <param name="width">Width in pixels; zero for samples without pixels.</param>
    /// <param name="height">Height in pixels; zero for samples without pixels.</param>
    /// <param name="format">How each sample is stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 1 to 16 for integers, 32 for floats.</param>
    /// <param name="channels">What each stored channel of a pixel holds, in storage order.</param>
    /// <param name="isPremultiplied">Whether the color channels are premultiplied by the alpha channel.</param>
    public PdfImageSamples(int width, int height, PdfImageSampleFormat format, int bitsPerComponent, PdfImageChannel[] channels, bool isPremultiplied)
        : this(AllocateBuffer(width, height, bitsPerComponent, channels), width, height, format, bitsPerComponent, channels, isPremultiplied)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageSamples"/> class over <paramref name="buffer"/>, whose
    /// start holds the rows; the rest of it is left alone.
    /// </summary>
    /// <param name="buffer">The memory of the rows, at least as long as they are.</param>
    /// <param name="width">Width in pixels; zero for samples without pixels.</param>
    /// <param name="height">Height in pixels; zero for samples without pixels.</param>
    /// <param name="format">How each sample is stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each sample: 1 to 16 for integers, 32 for floats.</param>
    /// <param name="channels">What each stored channel of a pixel holds, in storage order.</param>
    /// <param name="isPremultiplied">Whether the color channels are premultiplied by the alpha channel.</param>
    public PdfImageSamples(byte[] buffer, int width, int height, PdfImageSampleFormat format, int bitsPerComponent, PdfImageChannel[] channels, bool isPremultiplied)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must not be negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must not be negative.");
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

        if (isPremultiplied && Array.IndexOf(channels, PdfImageChannel.Alpha) < 0)
        {
            throw new ArgumentException("Premultiplied samples require an alpha channel.", nameof(isPremultiplied));
        }

        Width = width;
        Height = height;
        Format = format;
        BitsPerComponent = bitsPerComponent;
        _channels = channels;
        IsPremultiplied = isPremultiplied;
        RowBytes = GetRowBytes(width, channels.Length, bitsPerComponent);
        _length = RowBytes * height;

        if (buffer.Length < _length)
        {
            throw new ArgumentException("The buffer is shorter than the rows.", nameof(buffer));
        }

        _buffer = buffer;
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
    /// Gets whether the color channels are premultiplied by the alpha channel.
    /// </summary>
    public bool IsPremultiplied { get; }

    /// <inheritdoc />
    public int RowBytes { get; }

    /// <summary>
    /// Gets a read-only view over all rows, with no padding between rows.
    /// </summary>
    public ReadOnlySpan<byte> Pixels => _buffer.AsSpan(0, _length);

    /// <summary>
    /// Gets the memory of all rows, with no padding between rows.
    /// </summary>
    internal ReadOnlyMemory<byte> Memory => _buffer.AsMemory(0, _length);

    /// <inheritdoc />
    public Span<byte> GetRow(int rowIndex) => _buffer.AsSpan(rowIndex * RowBytes, RowBytes);

    /// <summary>
    /// Pins the sample buffer and returns the handle the caller must free.
    /// </summary>
    public GCHandle PinBuffer() => GCHandle.Alloc(_buffer, GCHandleType.Pinned);

    /// <summary>
    /// Returns a buffer for the rows of samples of the given dimensions and layout, empty when they are invalid so the
    /// constructor rejects them.
    /// </summary>
    private static byte[] AllocateBuffer(int width, int height, int bitsPerComponent, PdfImageChannel[]? channels)
    {
        if (width < 0 || height < 0 || bitsPerComponent < 1 || channels == null)
        {
            return Array.Empty<byte>();
        }

        int length = GetRowBytes(width, channels.Length, bitsPerComponent) * height;
#if NET5_0_OR_GREATER
        return GC.AllocateUninitializedArray<byte>(length);
#else
        return new byte[length];
#endif
    }

    private static int GetRowBytes(int width, int channelCount, int bitsPerComponent) => ((width * channelCount * bitsPerComponent) + 7) / 8;
}

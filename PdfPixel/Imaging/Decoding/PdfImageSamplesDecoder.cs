using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Objects;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Serves <see cref="PdfImageSamples"/> as one tile of colors and, when they hold alpha, one tile of alpha, drawn
/// with a paint whose color space the samples are in.
/// </summary>
public sealed class PdfImageSamplesDecoder : IPdfImageDecoder
{
    private readonly PdfImageSamples _samples;
    private readonly int[] _componentChannels;
    private readonly int? _alphaChannel;
    private readonly PdfImageSampleInfo _colorInfo;
    private readonly PdfImageSampleInfo? _alphaInfo;
    private PdfImageTile? _colorTile;
    private PdfImageTile? _alphaTile;

    /// <summary>
    /// Initializes a decoder of <paramref name="samples"/> drawn with <paramref name="paint"/>.
    /// </summary>
    /// <param name="samples">
    /// Integer samples holding each component of the paint's color space once, in any order, and at most one alpha
    /// channel.
    /// </param>
    /// <param name="paint">The paint whose color space the samples are in, composited with its alpha and blend mode.</param>
    /// <param name="interpolate">Whether the samples are interpolated where they are enlarged.</param>
    public PdfImageSamplesDecoder(PdfImageSamples samples, PdfPaint paint, bool interpolate)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        _samples = samples ?? throw new ArgumentNullException(nameof(samples));
        Interpolate = interpolate;

        if (samples.Format != PdfImageSampleFormat.Integer)
        {
            throw new NotSupportedException("Only integer samples can be drawn yet.");
        }

        PdfColorSpaceConverter colorSpace = paint.ColorSpace;
        (_componentChannels, _alphaChannel) = GetChannels(samples, colorSpace.Components);

        PdfIntegerSize size = new(samples.Width, samples.Height);
        PdfTileInfo grid = new(size, size);

        _colorInfo = new PdfImageSampleInfo(grid, descaleFactor: 1, samples.BitsPerComponent, colorSpace.Components, colorSpace);

        if (_alphaChannel != null)
        {
            _alphaInfo = new PdfImageSampleInfo(grid, descaleFactor: 1, samples.BitsPerComponent, componentCount: 1, PdfDeviceGrayColorSpaceConverter.Instance);
        }
    }

    /// <inheritdoc />
    public PdfImageMaskKind MaskKind
    {
        get
        {
            if (_alphaChannel == null)
            {
                return PdfImageMaskKind.None;
            }
            else if (_samples.IsPremultiplied)
            {
                return PdfImageMaskKind.PremultipliedSoftMaskInData;
            }
            else
            {
                return PdfImageMaskKind.SoftMaskInData;
            }
        }
    }

    /// <inheritdoc />
    public bool HasImageMask => false;

    /// <inheritdoc />
    public PdfRange[]? Decode => null;

    /// <inheritdoc />
    public bool Interpolate { get; }

    /// <inheritdoc />
    public int[]? ColorKeyMask => null;

    /// <inheritdoc />
    public float[]? Matte => null;

    /// <inheritdoc />
    public PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfImageCache cache, IPdfExecutionObserver? observer)
    {
        if (_colorTile == null)
        {
            _colorTile = CreateTile(_componentChannels);
        }

        if (_alphaChannel != null && _alphaTile == null)
        {
            _alphaTile = CreateTile([_alphaChannel.Value]);
        }

        return new PdfImageDecodeInfo(_colorInfo, _alphaInfo);
    }

    /// <inheritdoc />
    public PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer) => _colorTile;

    /// <inheritdoc />
    public PdfImageTile? GetAlphaTile(int tileIndex, IPdfExecutionObserver? observer) => _alphaTile;

    /// <inheritdoc />
    public void ReleaseTiles()
    {
    }

    /// <summary>
    /// Returns the channel holding each component, in component order, and the alpha channel, if any.
    /// </summary>
    private static (int[] ComponentChannels, int? AlphaChannel) GetChannels(PdfImageSamples samples, int componentCount)
    {
        IReadOnlyList<PdfImageChannel> channels = samples.Channels;
        var componentChannels = new int[componentCount];
        var foundComponents = new bool[componentCount];
        int? alphaChannel = null;

        for (int channel = 0; channel < channels.Count; channel++)
        {
            int? componentIndex = channels[channel].ComponentIndex;

            if (componentIndex == null && alphaChannel == null)
            {
                alphaChannel = channel;
            }
            else if (componentIndex != null && componentIndex.Value < componentCount && !foundComponents[componentIndex.Value])
            {
                componentChannels[componentIndex.Value] = channel;
                foundComponents[componentIndex.Value] = true;
            }
            else
            {
                throw new ArgumentException("The samples must hold each component of the paint's color space once and at most one alpha channel.", nameof(samples));
            }
        }

        if (Array.IndexOf(foundComponents, false) >= 0)
        {
            throw new ArgumentException("The samples must hold each component of the paint's color space once and at most one alpha channel.", nameof(samples));
        }

        return (componentChannels, alphaChannel);
    }

    /// <summary>
    /// Returns the tile of the samples' <paramref name="channels"/>, in that order: the samples themselves when they
    /// hold nothing else, otherwise a copy of those channels.
    /// </summary>
    private PdfImageTile CreateTile(int[] channels)
    {
        PdfImageSamples samples = _samples;
        int channelCount = samples.Channels.Count;
        PdfIntegerRectangle bounds = new(0, 0, samples.Width, samples.Height);

        if (IsIdentity(channels, channelCount))
        {
            return new PdfImageTile(samples.Memory, samples.RowBytes, bounds, 0, samples.BitsPerComponent, channelCount);
        }

        PdfImageRowChannelMapStep step = new(channelCount, channels, samples.Format, samples.BitsPerComponent);
        int rowStride = step.GetOutputLength(samples.RowBytes);
        var data = new byte[rowStride * samples.Height];
        ReadOnlySpan<byte> pixels = samples.Pixels;

        for (int row = 0; row < samples.Height; row++)
        {
            step.Process(pixels.Slice(row * samples.RowBytes, samples.RowBytes), data.AsSpan(row * rowStride, rowStride));
        }

        return new PdfImageTile(data, rowStride, bounds, 0, samples.BitsPerComponent, channels.Length);
    }

    private static bool IsIdentity(int[] channels, int channelCount)
    {
        if (channels.Length != channelCount)
        {
            return false;
        }

        for (int channel = 0; channel < channels.Length; channel++)
        {
            if (channels[channel] != channel)
            {
                return false;
            }
        }

        return true;
    }
}

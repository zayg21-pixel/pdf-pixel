using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// The pixels of a layer's result, read and written as runs of premultiplied float components followed by alpha, in
/// device coordinates of the layer's bounds. Samples of another layout are converted run by run: read into a copy, and
/// a run open for change written back when it ends.
/// </summary>
internal sealed class SnapLayerPixels
{
    private const int FloatBitsPerComponent = 32;
    private const int BitsPerByte = 8;

    private readonly PdfImageSamples _samples;
    private readonly int _left;
    private readonly int _top;
    private readonly int _pixelStride;
    private readonly int _samplePixelBytes;
    private readonly PdfImageSamplesConverter? _readConverter;
    private readonly PdfImageSamplesConverter? _writeConverter;
    private float[] _readValues = [];
    private float[] _runValues = [];

    /// <summary>
    /// Initializes the pixels of <paramref name="samples"/> covering <paramref name="bounds"/>, holding
    /// <paramref name="componentCount"/> components and alpha.
    /// </summary>
    /// <exception cref="NotSupportedException">The samples hold alpha that is not premultiplied.</exception>
    public SnapLayerPixels(PdfImageSamples samples, in PdfIntegerRectangle bounds, int componentCount)
    {
        _samples = samples ?? throw new ArgumentNullException(nameof(samples));

        if (samples.Width != bounds.Width || samples.Height != bounds.Height)
        {
            throw new ArgumentException("The samples must be the size of the layer.", nameof(samples));
        }

        _left = bounds.Left;
        _top = bounds.Top;
        _pixelStride = componentCount + 1;
        _samplePixelBytes = samples.Channels.Count * samples.BitsPerComponent / BitsPerByte;

        if (!IsNative(samples, componentCount))
        {
            if (HasAlpha(samples) && !samples.IsPremultiplied)
            {
                throw new NotSupportedException("Samples with alpha are drawn on only when it is premultiplied.");
            }

            PdfImageSamples layerLayout = new(0, 0, PdfImageSampleFormat.Float, FloatBitsPerComponent, CreateLayerChannels(componentCount), isPremultiplied: true);

            _readConverter = new PdfImageSamplesConverter(samples, layerLayout);
            _writeConverter = new PdfImageSamplesConverter(layerLayout, samples);
        }
    }

    /// <summary>
    /// Returns the run of <paramref name="count"/> pixels from <paramref name="left"/> on row <paramref name="y"/>
    /// to change; dispose it when done.
    /// </summary>
    public SnapLayerRun Open(int y, int left, int count)
    {
        int rowIndex = y - _top;
        int firstPixel = left - _left;

        if (_readConverter == null)
        {
            return new SnapLayerRun(_samples, rowIndex, firstPixel * _pixelStride, count * _pixelStride);
        }
        else
        {
            int valueCount = count * _pixelStride;
            Span<float> values = GetValues(ref _runValues, valueCount);

            _readConverter.Convert(GetSampleRun(rowIndex, firstPixel, count), MemoryMarshal.AsBytes(values));

            return new SnapLayerRun(_runValues, valueCount, this, rowIndex, firstPixel);
        }
    }

    /// <summary>
    /// Returns the run of <paramref name="count"/> pixels from <paramref name="left"/> on row <paramref name="y"/>
    /// to overwrite; dispose it when done. Its values are undefined until written.
    /// </summary>
    public SnapLayerRun Overwrite(int y, int left, int count)
    {
        int rowIndex = y - _top;
        int firstPixel = left - _left;

        if (_readConverter == null)
        {
            return new SnapLayerRun(_samples, rowIndex, firstPixel * _pixelStride, count * _pixelStride);
        }
        else
        {
            int valueCount = count * _pixelStride;

            GetValues(ref _runValues, valueCount);

            return new SnapLayerRun(_runValues, valueCount, this, rowIndex, firstPixel);
        }
    }

    /// <summary>
    /// Returns the run of <paramref name="count"/> pixels from <paramref name="left"/> on row <paramref name="y"/>,
    /// valid until the next read.
    /// </summary>
    public ReadOnlySpan<float> Read(int y, int left, int count)
    {
        int rowIndex = y - _top;
        int firstPixel = left - _left;

        if (_readConverter == null)
        {
            ReadOnlySpan<float> row = MemoryMarshal.Cast<byte, float>(_samples.GetRow(rowIndex));

            return row.Slice(firstPixel * _pixelStride, count * _pixelStride);
        }
        else
        {
            Span<float> values = GetValues(ref _readValues, count * _pixelStride);

            _readConverter.Convert(GetSampleRun(rowIndex, firstPixel, count), MemoryMarshal.AsBytes(values));

            return values;
        }
    }

    /// <summary>
    /// Writes <paramref name="values"/>, the converted copy of the pixels from <paramref name="firstPixel"/> on row
    /// <paramref name="rowIndex"/>, back into the samples.
    /// </summary>
    public void WriteBack(in ReadOnlySpan<float> values, int rowIndex, int firstPixel)
    {
        if (_writeConverter == null)
        {
            throw new InvalidOperationException("The pixels are changed in place.");
        }

        _writeConverter.Convert(MemoryMarshal.AsBytes(values), GetSampleRun(rowIndex, firstPixel, values.Length / _pixelStride));
    }

    private Span<byte> GetSampleRun(int rowIndex, int firstPixel, int count)
        => _samples.GetRow(rowIndex).Slice(firstPixel * _samplePixelBytes, count * _samplePixelBytes);

    private static Span<float> GetValues(ref float[] values, int length)
    {
        if (values.Length < length)
        {
            values = new float[length];
        }

        return values.AsSpan(0, length);
    }

    private static bool IsNative(PdfImageSamples samples, int componentCount)
    {
        if (samples.Format != PdfImageSampleFormat.Float
            || samples.BitsPerComponent != FloatBitsPerComponent
            || !samples.IsPremultiplied
            || samples.Channels.Count != componentCount + 1)
        {
            return false;
        }

        for (int component = 0; component < componentCount; component++)
        {
            if (samples.Channels[component] != PdfImageChannel.Component(component))
            {
                return false;
            }
        }

        return samples.Channels[componentCount].IsAlpha;
    }

    private static bool HasAlpha(PdfImageSamples samples)
    {
        IReadOnlyList<PdfImageChannel> channels = samples.Channels;

        for (int index = 0; index < channels.Count; index++)
        {
            if (channels[index].IsAlpha)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the channels of a layer's own layout: its components, then alpha.
    /// </summary>
    public static PdfImageChannel[] CreateLayerChannels(int componentCount)
    {
        var channels = new PdfImageChannel[componentCount + 1];

        for (int component = 0; component < componentCount; component++)
        {
            channels[component] = PdfImageChannel.Component(component);
        }

        channels[componentCount] = PdfImageChannel.Alpha;

        return channels;
    }
}

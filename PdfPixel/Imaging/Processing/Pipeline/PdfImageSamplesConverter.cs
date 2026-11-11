using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Converts pixels from the layout of one <see cref="PdfImageSamples"/> into the layout of another, whole rows or runs
/// of any number of pixels, through the chain of row processing steps the two layouts need. A source without alpha is
/// opaque; a target without alpha receives premultiplied colors, as composited over black.
/// </summary>
public sealed class PdfImageSamplesConverter
{
    private const int ByteBitsPerComponent = 8;
    private const int ShortBitsPerComponent = 16;
    private const int FloatBitsPerComponent = 32;

    private readonly IPdfImageRowProcessingStep[] _steps;
    private byte[] _buffer = [];

    /// <summary>
    /// Initializes a converter from the layout of <paramref name="source"/> to the layout of <paramref name="target"/>:
    /// their format, bit depth, channels and premultiplication.
    /// </summary>
    /// <exception cref="ArgumentException">The target holds a component the source does not.</exception>
    /// <exception cref="NotSupportedException">One of the layouts holds integer samples of other than 8 or 16 bits.</exception>
    public PdfImageSamplesConverter(PdfImageSamples source, PdfImageSamples target)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (!IsConvertible(source) || !IsConvertible(target))
        {
            throw new NotSupportedException("Integer samples are converted only at 8 or 16 bits.");
        }

        foreach (PdfImageChannel channel in target.Channels)
        {
            if (!channel.IsAlpha && IndexOf(source.Channels, channel) < 0)
            {
                throw new ArgumentException($"The source samples hold no channel {channel}.", nameof(target));
            }
        }

        _steps = CreateSteps(source, target);
    }

    /// <summary>
    /// Converts the pixels of <paramref name="source"/>, in the source layout, into <paramref name="target"/>, in the
    /// target layout; <paramref name="target"/> holds at least as many pixels.
    /// </summary>
    public void Convert(in ReadOnlySpan<byte> source, in Span<byte> target)
    {
        if (_steps.Length == 0)
        {
            source.CopyTo(target);
        }
        else
        {
            RunSteps(source, target);
        }
    }

    /// <summary>
    /// Runs the steps over <paramref name="source"/>: the first reads it, every step but the last writes into the
    /// buffer, its output starting where its input does, and the last writes into <paramref name="target"/>.
    /// </summary>
    private void RunSteps(in ReadOnlySpan<byte> source, in Span<byte> target)
    {
        int lastStep = _steps.Length - 1;
        int length = source.Length;
        int longest = 0;

        for (int stepIndex = 0; stepIndex < lastStep; stepIndex++)
        {
            length = _steps[stepIndex].GetOutputLength(length);
            longest = Math.Max(longest, length);
        }

        if (_buffer.Length < longest)
        {
            _buffer = new byte[longest];
        }

        ReadOnlySpan<byte> input = source;
        length = source.Length;

        for (int stepIndex = 0; stepIndex < lastStep; stepIndex++)
        {
            length = _steps[stepIndex].GetOutputLength(length);
            Span<byte> output = _buffer.AsSpan(0, length);
            _steps[stepIndex].Process(input, output);
            input = output;
        }

        _steps[lastStep].Process(input, target);
    }

    /// <summary>
    /// Returns the steps from the layout of <paramref name="source"/> to the layout of <paramref name="target"/>: to
    /// float, an opaque alpha the source lacks, a change of premultiplication on the target's components followed by
    /// alpha, to integer, and the target's channel order; each only when the layouts need it. Integers of the same bit
    /// depth on both sides stay integers throughout.
    /// </summary>
    private static IPdfImageRowProcessingStep[] CreateSteps(PdfImageSamples source, PdfImageSamples target)
    {
        List<IPdfImageRowProcessingStep> steps = [];
        List<PdfImageChannel> channels = [.. source.Channels];
        bool targetHasAlpha = IndexOf(target.Channels, PdfImageChannel.Alpha) >= 0;
        bool keepsIntegers = source.Format == PdfImageSampleFormat.Integer
            && target.Format == PdfImageSampleFormat.Integer
            && source.BitsPerComponent == target.BitsPerComponent;
        var stepFormat = PdfImageSampleFormat.Float;
        int stepBitsPerComponent = FloatBitsPerComponent;

        if (keepsIntegers)
        {
            stepFormat = PdfImageSampleFormat.Integer;
            stepBitsPerComponent = source.BitsPerComponent;
        }
        else if (source.Format == PdfImageSampleFormat.Integer)
        {
            steps.Add(new PdfImageRowByteToFloatStep(source.BitsPerComponent));
        }

        if (targetHasAlpha && IndexOf(channels, PdfImageChannel.Alpha) < 0)
        {
            steps.Add(new PdfImageRowOpaqueAlphaStep(channels.Count, stepFormat, stepBitsPerComponent));
            channels.Add(PdfImageChannel.Alpha);
        }

        IPdfImageRowProcessingStep? premultiplication = CreatePremultiplicationStep(source, target, targetHasAlpha, stepFormat, stepBitsPerComponent);

        if (premultiplication != null)
        {
            List<PdfImageChannel> componentsAndAlpha = [];

            foreach (PdfImageChannel channel in target.Channels)
            {
                if (!channel.IsAlpha)
                {
                    componentsAndAlpha.Add(channel);
                }
            }

            componentsAndAlpha.Add(PdfImageChannel.Alpha);
            AddChannelMap(steps, channels, componentsAndAlpha, stepFormat, stepBitsPerComponent);
            steps.Add(premultiplication);
        }

        if (!keepsIntegers && target.Format == PdfImageSampleFormat.Integer)
        {
            steps.Add(new PdfImageRowFloatToByteStep(target.BitsPerComponent));
        }

        AddChannelMap(steps, channels, target.Channels, target.Format, target.BitsPerComponent);

        return [.. steps];
    }

    /// <summary>
    /// Returns the step changing the premultiplication of a source with alpha into the target's, or into
    /// premultiplied colors for a target without alpha; <see langword="null"/> when none is needed.
    /// </summary>
    private static IPdfImageRowProcessingStep? CreatePremultiplicationStep(
        PdfImageSamples source,
        PdfImageSamples target,
        bool targetHasAlpha,
        PdfImageSampleFormat stepFormat,
        int stepBitsPerComponent)
    {
        int componentCount = target.Channels.Count;

        if (targetHasAlpha)
        {
            componentCount--;
        }

        bool sourceHasAlpha = IndexOf(source.Channels, PdfImageChannel.Alpha) >= 0;
        bool targetIsPremultiplied = target.IsPremultiplied || !targetHasAlpha;

        if (!sourceHasAlpha || source.IsPremultiplied == targetIsPremultiplied)
        {
            return null;
        }
        else if (targetIsPremultiplied)
        {
            return new PdfImageRowPremultiplyStep(componentCount, stepFormat, stepBitsPerComponent);
        }
        else
        {
            return new PdfImageRowUnpremultiplyStep(componentCount, stepFormat, stepBitsPerComponent);
        }
    }

    /// <summary>
    /// Adds the step reordering <paramref name="channels"/>, the pixels' channels so far, into
    /// <paramref name="targetChannels"/>, unless they are already in that order.
    /// </summary>
    private static void AddChannelMap(
        List<IPdfImageRowProcessingStep> steps,
        List<PdfImageChannel> channels,
        IReadOnlyList<PdfImageChannel> targetChannels,
        PdfImageSampleFormat format,
        int bitsPerComponent)
    {
        var inputChannels = new int[targetChannels.Count];
        bool isIdentity = targetChannels.Count == channels.Count;

        for (int channel = 0; channel < inputChannels.Length; channel++)
        {
            inputChannels[channel] = IndexOf(channels, targetChannels[channel]);
            isIdentity = isIdentity && inputChannels[channel] == channel;
        }

        if (!isIdentity)
        {
            steps.Add(new PdfImageRowChannelMapStep(channels.Count, inputChannels, format, bitsPerComponent));
            channels.Clear();
            channels.AddRange(targetChannels);
        }
    }

    private static bool IsConvertible(PdfImageSamples samples)
    {
        return samples.Format == PdfImageSampleFormat.Float
            || samples.BitsPerComponent == ByteBitsPerComponent
            || samples.BitsPerComponent == ShortBitsPerComponent;
    }

    private static int IndexOf(IReadOnlyList<PdfImageChannel> channels, in PdfImageChannel channel)
    {
        for (int index = 0; index < channels.Count; index++)
        {
            if (channels[index] == channel)
            {
                return index;
            }
        }

        return -1;
    }
}

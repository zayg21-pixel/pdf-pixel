using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Appends an alpha channel from an <see cref="IPdfAlphaRowSource"/> to float pixels. Pixels of a row
/// the source cannot serve are opaque.
/// </summary>
public sealed class PdfImageRowAlphaPlaneStep : IPdfImageRowProcessingStep
{
    private const float OpaqueAlpha = 1f;

    private readonly IPdfAlphaRowSource _alphaSource;
    private readonly int _width;
    private readonly int _componentCount;
    private readonly float[] _buffer;

    /// <summary>
    /// Initializes a step appending alpha to rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="alphaSource">Source of the alpha rows, indexed like the input rows.</param>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="componentCount">Number of components in each input pixel.</param>
    public PdfImageRowAlphaPlaneStep(IPdfAlphaRowSource alphaSource, int width, int componentCount)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        _alphaSource = alphaSource ?? throw new ArgumentNullException(nameof(alphaSource));
        _width = width;
        _componentCount = componentCount;
        _buffer = new float[width * (componentCount + 1)];
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);

        if (samples.Length < _width * _componentCount)
        {
            throw new ArgumentException("The input row holds fewer samples than the step reads.", nameof(input));
        }

        ReadOnlySpan<float> alphaRow = _alphaSource.GetRow(inputRowIndex);

        if (alphaRow.Length < _width)
        {
            AppendOpaqueAlpha(samples);
        }
        else
        {
            AppendAlpha(samples, alphaRow);
        }

        return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), inputRowIndex, hasNextRow: false);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AppendAlpha(in ReadOnlySpan<float> samples, in ReadOnlySpan<float> alphaRow)
    {
        ref float sourceReference = ref MemoryMarshal.GetReference(samples);
        ref float alphaReference = ref MemoryMarshal.GetReference(alphaRow);
        ref float destinationReference = ref _buffer[0];
        int sourceIndex = 0;
        int destinationIndex = 0;

        for (int x = 0; x < _width; x++)
        {
            for (int component = 0; component < _componentCount; component++)
            {
                Unsafe.Add(ref destinationReference, destinationIndex++) = Unsafe.Add(ref sourceReference, sourceIndex++);
            }

            Unsafe.Add(ref destinationReference, destinationIndex++) = Unsafe.Add(ref alphaReference, x);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AppendOpaqueAlpha(in ReadOnlySpan<float> samples)
    {
        ref float sourceReference = ref MemoryMarshal.GetReference(samples);
        ref float destinationReference = ref _buffer[0];
        int sourceIndex = 0;
        int destinationIndex = 0;

        for (int x = 0; x < _width; x++)
        {
            for (int component = 0; component < _componentCount; component++)
            {
                Unsafe.Add(ref destinationReference, destinationIndex++) = Unsafe.Add(ref sourceReference, sourceIndex++);
            }

            Unsafe.Add(ref destinationReference, destinationIndex++) = OpaqueAlpha;
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Recovers float colors preblended with a matte, as c = m + (c' - m) / a, from pixels whose alpha channel
/// follows their color components. Pixels with zero alpha take the matte.
/// </summary>
public sealed class PdfImageRowMatteStep : IPdfImageRowProcessingStep
{
    private readonly int _width;
    private readonly float[] _matte;
    private readonly float[] _buffer;

    /// <summary>
    /// Initializes a step recovering rows of <paramref name="width"/> pixels.
    /// </summary>
    /// <param name="width">Number of pixels in each row.</param>
    /// <param name="matte">Matte components, in the range and order of the pixel's color components.</param>
    public PdfImageRowMatteStep(int width, float[] matte)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (matte == null || matte.Length == 0)
        {
            throw new ArgumentException("At least one matte component is required.", nameof(matte));
        }

        _width = width;
        _matte = matte;
        _buffer = new float[width * (matte.Length + 1)];
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

        if (samples.Length < _buffer.Length)
        {
            throw new ArgumentException("The input row holds fewer samples than the step recovers.", nameof(input));
        }

        int componentCount = _matte.Length;
        int stride = componentCount + 1;
        ref float sourceReference = ref MemoryMarshal.GetReference(samples);
        ref float destinationReference = ref _buffer[0];
        ref float matteReference = ref _matte[0];

        for (int x = 0; x < _width; x++)
        {
            int offset = x * stride;
            float alpha = Unsafe.Add(ref sourceReference, offset + componentCount);

            // A zero reciprocal collapses the expression to the matte, which is what zero alpha takes.
            float inverseAlpha = (alpha == 0f) ? 0f : 1f / alpha;

            for (int component = 0; component < componentCount; component++)
            {
                float matte = Unsafe.Add(ref matteReference, component);
                Unsafe.Add(ref destinationReference, offset + component) = matte + ((Unsafe.Add(ref sourceReference, offset + component) - matte) * inverseAlpha);
            }

            Unsafe.Add(ref destinationReference, offset + componentCount) = alpha;
        }

        return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), inputRowIndex, hasNextRow: false);
    }
}

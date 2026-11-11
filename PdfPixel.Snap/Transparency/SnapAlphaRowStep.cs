using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// Keeps only the alpha that follows the float components of each pixel.
/// </summary>
internal sealed class SnapAlphaRowStep : IPdfImageRowProcessingStep
{
    private readonly int _componentCount;

    public SnapAlphaRowStep(int componentCount) => _componentCount = componentCount;

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength / (_componentCount + 1);

    /// <inheritdoc />
    public PdfImageOutputRow GetOutputRow(int inputRowIndex) => new(inputRowIndex, hasNextRow: false);

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex) => Process(input, output);

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<float> values = MemoryMarshal.Cast<byte, float>(output);
        int stride = _componentCount + 1;
        ref float alpha = ref Unsafe.Add(ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(input)), _componentCount);
        ref float value = ref MemoryMarshal.GetReference(values);

        for (int index = 0; index < values.Length; index++)
        {
            value = alpha;
            alpha = ref Unsafe.Add(ref alpha, stride);
            value = ref Unsafe.Add(ref value, 1);
        }
    }
}

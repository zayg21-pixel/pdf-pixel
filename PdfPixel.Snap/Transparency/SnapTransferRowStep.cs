using PdfPixel.Functions;
using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// Passes each float value through a transfer function, clamped to 0 to 1; a function giving no output keeps the value.
/// </summary>
internal sealed class SnapTransferRowStep : IPdfImageRowProcessingStep
{
    private readonly PdfFunction _function;

    public SnapTransferRowStep(PdfFunction function) => _function = function ?? throw new ArgumentNullException(nameof(function));

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength;

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
        ReadOnlySpan<float> values = MemoryMarshal.Cast<byte, float>(input);
        ref float source = ref MemoryMarshal.GetReference(values);
        ref float result = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(output));

        // TODO: evaluate the function through a lookup, the way the ICC TRC is, instead of once per value.
        for (int index = 0; index < values.Length; index++)
        {
            float value = source;
            ReadOnlySpan<float> transferred = _function.Evaluate(value);

            if (!transferred.IsEmpty)
            {
                value = transferred[0];
            }

            result = Math.Max(0, Math.Min(1, value));
            source = ref Unsafe.Add(ref source, 1);
            result = ref Unsafe.Add(ref result, 1);
        }
    }
}

using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// Turns premultiplied pixels followed by alpha into luminosity times alpha; CMYK, where luminosity is not linear, is
/// unpremultiplied first.
/// </summary>
internal sealed class SnapLuminosityRowStep : IPdfImageRowProcessingStep
{
    private const float RedWeight = 0.30f;
    private const float GreenWeight = 0.59f;
    private const float BlueWeight = 0.11f;

    private readonly SnapBlendingColorModel _colorModel;
    private readonly int _stride;

    public SnapLuminosityRowStep(SnapBlendingColorModel colorModel)
    {
        switch (colorModel)
        {
            case SnapBlendingColorModel.Gray:
            {
                _stride = 2;
                break;
            }
            case SnapBlendingColorModel.Rgb:
            {
                _stride = 4;
                break;
            }
            default:
            {
                _stride = 5;
                break;
            }
        }

        _colorModel = colorModel;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => inputLength / _stride;

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
        ref float pixel = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, float>(input));
        ref float value = ref MemoryMarshal.GetReference(values);

        switch (_colorModel)
        {
            case SnapBlendingColorModel.Gray:
            {
                for (int index = 0; index < values.Length; index++)
                {
                    value = Math.Max(0, Math.Min(1, pixel));
                    pixel = ref Unsafe.Add(ref pixel, _stride);
                    value = ref Unsafe.Add(ref value, 1);
                }

                break;
            }
            case SnapBlendingColorModel.Rgb:
            {
                for (int index = 0; index < values.Length; index++)
                {
                    float luminosity = (RedWeight * pixel)
                        + (GreenWeight * Unsafe.Add(ref pixel, 1))
                        + (BlueWeight * Unsafe.Add(ref pixel, 2));

                    value = Math.Max(0, Math.Min(1, luminosity));
                    pixel = ref Unsafe.Add(ref pixel, _stride);
                    value = ref Unsafe.Add(ref value, 1);
                }

                break;
            }
            default:
            {
                for (int index = 0; index < values.Length; index++)
                {
                    float alpha = Unsafe.Add(ref pixel, 4);
                    float inverseAlpha = (alpha > 0) ? 1 / alpha : 0;
                    float premultipliedCoverage = (RedWeight * pixel)
                        + (GreenWeight * Unsafe.Add(ref pixel, 1))
                        + (BlueWeight * Unsafe.Add(ref pixel, 2))
                        + Unsafe.Add(ref pixel, 3);
                    float coverage = premultipliedCoverage * inverseAlpha;

                    value = Math.Max(0, Math.Min(1, alpha * (1 - Math.Min(1, coverage))));
                    pixel = ref Unsafe.Add(ref pixel, _stride);
                    value = ref Unsafe.Add(ref value, 1);
                }

                break;
            }
        }
    }
}

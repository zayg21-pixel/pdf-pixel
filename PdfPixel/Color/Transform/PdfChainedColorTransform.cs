using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Converts the components of a color through a <see cref="ChainedColorTransform"/> of up to four components.
/// </summary>
public sealed class PdfChainedColorTransform : IPdfColorTransform
{
    private const int MaximumComponentCount = 4;

    private readonly ChainedColorTransform _colorTransform;

    /// <summary>
    /// Initializes a transform converting through <paramref name="colorTransform"/>.
    /// </summary>
    /// <param name="colorTransform">Chain the components are converted through.</param>
    /// <param name="inputComponentCount">Number of components the transform reads, 0 to 4.</param>
    /// <param name="outputComponentCount">Number of components the transform produces, 1 to 4.</param>
    public PdfChainedColorTransform(ChainedColorTransform colorTransform, int inputComponentCount, int outputComponentCount)
    {
        if (inputComponentCount < 0 || inputComponentCount > MaximumComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(inputComponentCount), "Input component count must be 0 to 4.");
        }

        if (outputComponentCount < 1 || outputComponentCount > MaximumComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(outputComponentCount), "Output component count must be 1 to 4.");
        }

        _colorTransform = colorTransform ?? throw new ArgumentNullException(nameof(colorTransform));
        InputComponentCount = inputComponentCount;
        OutputComponentCount = outputComponentCount;
    }

    /// <inheritdoc />
    public int InputComponentCount { get; }

    /// <inheritdoc />
    public int OutputComponentCount { get; }

    /// <inheritdoc />
    public void Transform(ref float input, ref float output)
    {
        Vector4 color = _colorTransform.Transform(LoadWithOnePadding(ref input, InputComponentCount));

        StoreComponents(color, ref output, OutputComponentCount);
    }

    /// <summary>
    /// Returns the <paramref name="count"/> components at <paramref name="input"/> as a vector, the components it
    /// lacks set to 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 LoadWithOnePadding(ref float input, int count)
    {
        switch (count)
        {
            case 0:
            {
                return Vector4.One;
            }
            case 1:
            {
                return new Vector4(input, 1f, 1f, 1f);
            }
            case 2:
            {
                return new Vector4(input, Unsafe.Add(ref input, 1), 1f, 1f);
            }
            case 3:
            {
                return new Vector4(input, Unsafe.Add(ref input, 1), Unsafe.Add(ref input, 2), 1f);
            }
            default:
            {
                return Unsafe.As<float, Vector4>(ref input);
            }
        }
    }

    /// <summary>
    /// Writes the first <paramref name="count"/> components of <paramref name="color"/> to <paramref name="output"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreComponents(Vector4 color, ref float output, int count)
    {
        switch (count)
        {
            case 1:
            {
                output = color.X;
                break;
            }
            case 2:
            {
                output = color.X;
                Unsafe.Add(ref output, 1) = color.Y;
                break;
            }
            case 3:
            {
                output = color.X;
                Unsafe.Add(ref output, 1) = color.Y;
                Unsafe.Add(ref output, 2) = color.Z;
                break;
            }
            default:
            {
                Unsafe.As<float, Vector4>(ref output) = color;
                break;
            }
        }
    }
}

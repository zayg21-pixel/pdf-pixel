using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Converts the components of one color through a chain of <see cref="IPdfColorTransform"/>, in order. Between steps
/// the color goes through two buffers the sampler owns, sized to the widest step; the first step reads the caller's
/// input and the last one writes the caller's output.
/// </summary>
public sealed class PdfColorSampler
{
    private readonly float[] _first;
    private readonly float[] _second;

    /// <summary>
    /// Initializes a sampler running <paramref name="transforms"/> in order.
    /// </summary>
    /// <param name="transforms">Transforms each color runs through, in order.</param>
    public PdfColorSampler(IPdfColorTransform[] transforms)
    {
        Transforms = transforms ?? throw new ArgumentNullException(nameof(transforms));

        if (transforms.Length == 0)
        {
            throw new ArgumentException("At least one transform is required.", nameof(transforms));
        }

        int widestOutput = transforms[0].OutputComponentCount;

        for (int index = 1; index < transforms.Length; index++)
        {
            if (transforms[index].InputComponentCount != transforms[index - 1].OutputComponentCount)
            {
                throw new ArgumentException($"Transform {index} reads a different number of components than the transform before it produces.", nameof(transforms));
            }

            widestOutput = Math.Max(widestOutput, transforms[index].OutputComponentCount);
        }

        _first = new float[widestOutput];
        _second = new float[widestOutput];
    }

    /// <summary>
    /// Gets the transforms each color runs through, in order.
    /// </summary>
    public IPdfColorTransform[] Transforms { get; }

    /// <summary>
    /// Gets the number of components <see cref="Sample"/> reads.
    /// </summary>
    public int InputComponentCount => Transforms[0].InputComponentCount;

    /// <summary>
    /// Gets the number of components <see cref="Sample"/> writes.
    /// </summary>
    public int OutputComponentCount => Transforms[Transforms.Length - 1].OutputComponentCount;

    /// <summary>
    /// Converts the <see cref="InputComponentCount"/> components at <paramref name="input"/> through every transform
    /// into the <see cref="OutputComponentCount"/> components at <paramref name="output"/>.
    /// </summary>
    /// <param name="input">The first component to convert.</param>
    /// <param name="output">The first component receiving the converted color.</param>
    public void Sample(ref float input, ref float output)
    {
        IPdfColorTransform[] transforms = Transforms;
        int lastIndex = transforms.Length - 1;

        if (lastIndex == 0)
        {
            transforms[0].Transform(ref input, ref output);
        }
        else
        {
            ref float current = ref MemoryMarshal.GetReference(_first.AsSpan());
            ref float next = ref MemoryMarshal.GetReference(_second.AsSpan());

            transforms[0].Transform(ref input, ref current);

            for (int index = 1; index < lastIndex; index++)
            {
                transforms[index].Transform(ref current, ref next);

                ref float written = ref next;
                next = ref current;
                current = ref written;
            }

            transforms[lastIndex].Transform(ref current, ref output);
        }
    }
}

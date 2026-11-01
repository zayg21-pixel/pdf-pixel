using System;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Converts the components of one color through a chain of <see cref="IPdfColorTransform"/>, in order.
/// </summary>
public sealed class PdfColorSampler
{
    /// <summary>
    /// Initializes a sampler running <paramref name="transforms"/> in order.
    /// </summary>
    /// <param name="transforms">Transforms each color runs through, in order.</param>
    public PdfColorSampler(IPdfColorTransform[] transforms)
    {
        Transforms = transforms ?? throw new ArgumentNullException(nameof(transforms));

        for (int index = 1; index < transforms.Length; index++)
        {
            if (transforms[index].InputComponentCount != transforms[index - 1].OutputComponentCount)
            {
                throw new ArgumentException($"Transform {index} reads a different number of components than the transform before it produces.", nameof(transforms));
            }
        }
    }

    /// <summary>
    /// Gets the transforms each color runs through, in order.
    /// </summary>
    public IPdfColorTransform[] Transforms { get; }

    /// <summary>
    /// Converts the components in <paramref name="input"/> through every transform.
    /// </summary>
    /// <param name="input">Components to convert.</param>
    /// <returns>The converted components.</returns>
    public ReadOnlySpan<float> Sample(in ReadOnlySpan<float> input)
    {
        ReadOnlySpan<float> components = input;

        foreach (IPdfColorTransform transform in Transforms)
        {
            components = transform.Transform(components);
        }

        return components;
    }
}

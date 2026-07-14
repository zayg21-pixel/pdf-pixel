using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Represents a composite color transform that applies a sequence of <see cref="IColorTransform"/> operations in order.
/// </summary>
public sealed class ChainedColorTransform : IColorTransform
{
    private readonly IColorTransform[] _transforms;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChainedColorTransform"/> class with the specified transforms.
    /// Flattens any nested <see cref="ChainedColorTransform"/> instances for efficiency.
    /// </summary>
    /// <param name="transforms">The color transforms to chain together.</param>
    public ChainedColorTransform(
        params IColorTransform?[] transforms)
    {
        transforms ??= Array.Empty<IColorTransform>();
        List<IColorTransform> flattenedTransforms = [];

        foreach (IColorTransform? transform in transforms)
        {
            // Flatten nested chains to avoid unnecessary nesting and improve performance.
            if (transform is ChainedColorTransform chainedTransform)
            {
                flattenedTransforms.AddRange(chainedTransform._transforms);
            }
            else if (transform != null)
            {
                flattenedTransforms.Add(transform);
            }
        }

        _transforms = flattenedTransforms.Where(x => !x.IsIdentity).ToArray();
        IsIdentity = _transforms.Length == 0;
    }

    /// <inheritdoc />
    public bool IsIdentity { get; }

    /// <summary>
    /// Applies the chained color transforms to the specified color vector in sequence.
    /// </summary>
    /// <param name="color">The input color as a <see cref="Vector4"/>.</param>
    /// <returns>The transformed color as a <see cref="Vector4"/> after all chained transforms are applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        if (IsIdentity)
        {
            return color;
        }

        for (int i = 0; i < _transforms.Length; i++)
        {
            color = _transforms[i].Transform(color);
        }

        return color;
    }
}

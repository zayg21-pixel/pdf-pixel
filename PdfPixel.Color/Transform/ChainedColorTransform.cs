using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Represents a composite color transform that applies a sequence of <see cref="IColorTransform"/> operations in order.
/// </summary>
public sealed class ChainedColorTransform : IColorTransform
{
    private readonly IColorTransform[] _transforms;
    private readonly Func<Vector4, Vector4> _compiled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChainedColorTransform"/> class with the specified transforms.
    /// Flattens any nested <see cref="ChainedColorTransform"/> instances for efficiency.
    /// </summary>
    /// <param name="transforms">The color transforms to chain together.</param>
    public ChainedColorTransform(
        params IColorTransform[] transforms)
    {
        transforms ??= Array.Empty<IColorTransform>();
        List<IColorTransform> flattenedTransforms = [];

        foreach (IColorTransform transform in transforms)
        {
            // Flatten nested chains to avoid unnecessary nesting and improve performance.
            if (transform is ChainedColorTransform chainedTransform)
            {
                flattenedTransforms.AddRange(chainedTransform._transforms);
            }
            else
            {
                flattenedTransforms.Add(transform);
            }
        }

        _transforms = flattenedTransforms.Where(x => x?.IsIdentity == false).ToArray();
        IsIdentity = _transforms.Length == 0;
        _compiled = CompilePipeline(_transforms);
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

        return _compiled(color);
    }

    /// <summary>
    /// Builds a single compiled delegate that calls each concrete transform's
    /// <see cref="IColorTransform.Transform"/> method directly, eliminating per-pixel
    /// interface dispatch and loop overhead.
    /// </summary>
    private static Func<Vector4, Vector4> CompilePipeline(IColorTransform[] transforms)
    {
        if (transforms.Length == 0)
        {
            return static color => color;
        }

        ParameterExpression colorParam = Expression.Parameter(typeof(Vector4), "color");
        Expression body = colorParam;

        for (int i = 0; i < transforms.Length; i++)
        {
            Type concreteType = transforms[i].GetType();
            System.Reflection.MethodInfo? transformMethod = concreteType.GetMethod(
                nameof(IColorTransform.Transform),
                new[] { typeof(Vector4) });

            if (transformMethod == null)
            {
                throw new NotSupportedException(
                    $"Color transform type '{concreteType.Name}' does not expose a public Transform(Vector4) method.");
            }

            body = Expression.Call(
                Expression.Constant(transforms[i], concreteType),
                transformMethod,
                body);
        }

        return Expression.Lambda<Func<Vector4, Vector4>>(body, colorParam).Compile();
    }
}

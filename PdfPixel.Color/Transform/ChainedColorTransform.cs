using System;
using System.Collections.Generic;
#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfPixel.Color.Icc.Transform;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Represents a composite color transform that applies a sequence of <see cref="IColorTransform"/> operations in order.
/// </summary>
public sealed class ChainedColorTransform : IColorTransform
{
    private static readonly MethodInfo TransformMethod =
        typeof(IColorTransform).GetMethod(nameof(IColorTransform.Transform))!;

    private readonly IColorTransform[] _transforms;
    private readonly Func<Vector4, Vector4> _compiled;

    /// <summary>
    /// Optional hook for external <see cref="IColorTransform"/> implementations.
    /// Returns the concrete <see cref="Type"/> for the given transform so the compiled
    /// pipeline can call it directly without dynamic reflection. Return <c>null</c> to
    /// fall back to interface dispatch.
    /// </summary>
    public static ResolveTransformTypeDelegate? ResolveTransformType { get; set; }

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
    /// Returns the concrete type for built-in transforms. For external implementations,
    /// delegates to <see cref="ResolveTransformType"/>. Returns <c>null</c> if the type
    /// cannot be resolved statically — the pipeline will use interface dispatch in that case.
    /// </summary>
#if !NETSTANDARD2_0
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
    private static Type? GetConcreteType(IColorTransform transform)
    {
        switch (transform)
        {
            case PerChannelTrcTransform:
                return typeof(PerChannelTrcTransform);

            case ClutTransform:
                return typeof(ClutTransform);

            case MatrixColorTransform:
                return typeof(MatrixColorTransform);

            case FunctionColorTransform:
                return typeof(FunctionColorTransform);

            case CmykColorTransform:
                return typeof(CmykColorTransform);

            case ChainedColorTransform:
                return typeof(ChainedColorTransform);

            default:
                return ResolveTransformType?.Invoke(transform);
        }
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
            Type? concreteType = GetConcreteType(transforms[i]);

            if (concreteType != null)
            {
                MethodInfo? transformMethod = concreteType.GetMethod(
                    nameof(IColorTransform.Transform),
                    new[] { typeof(Vector4) });

                if (transformMethod != null)
                {
                    body = Expression.Call(
                        Expression.Constant(transforms[i], concreteType),
                        transformMethod,
                        body);
                    continue;
                }
            }

            body = Expression.Call(
                Expression.Constant(transforms[i], typeof(IColorTransform)),
                TransformMethod,
                body);
        }

        return Expression.Lambda<Func<Vector4, Vector4>>(body, colorParam).Compile();
    }
}

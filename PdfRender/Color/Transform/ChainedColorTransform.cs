using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PdfRender.Color.Transform;

/// <summary>
/// Represents a composite color transform that applies a sequence of <see cref="IColorTransform"/> operations in order.
/// </summary>
internal sealed class ChainedColorTransform : IColorTransform
{
    private readonly IColorTransform[] _transforms;
    private readonly bool _isIdentity;
    private readonly Lazy<PixelProcessorFunction> _processorFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChainedColorTransform"/> class with the specified transforms.
    /// Flattens any nested <see cref="ChainedColorTransform"/> instances for efficiency.
    /// </summary>
    /// <param name="transforms">The color transforms to chain together.</param>
    public ChainedColorTransform(params IColorTransform[] transforms)
    {
        var flattenedTransforms = new List<IColorTransform>();

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

        _transforms = flattenedTransforms.Where(x => x != null && !x.IsIdentity).ToArray();
        _isIdentity = _transforms.Length == 0;
        _processorFunction = new Lazy<PixelProcessorFunction>(BuildCallback, isThreadSafe: false);
    }

    public bool IsIdentity => _isIdentity;

    /// <summary>
    /// Applies the chained color transforms to the specified color vector in sequence.
    /// </summary>
    /// <param name="color">The input color as a <see cref="Vector4"/>.</param>
    /// <returns>The transformed color as a <see cref="Vector4"/> after all chained transforms are applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        return _processorFunction.Value(color);
    }

    private PixelProcessorFunction BuildCallback()
    {
        if (_isIdentity)
        {
            return static (Vector4 input) => input;
        }

        return (Vector4 input) =>
        {
            Vector4 result = input;
            foreach (IColorTransform transform in _transforms)
            {
                result = transform.Transform(result);
            }
            return result;
        };

        // TODO: Benchmark this against the expression tree approach for long chains.
        //// For small chains, hand-compose a straight-line delegate to eliminate expression overhead entirely.
        //// This also helps JIT devirtualize and inline calls for sealed types.
        //int length = _transforms.Length;

        //ParameterExpression inputParam = Expression.Parameter(typeof(Vector4), "input");
        //Expression body = inputParam;

        //for (int i = 0; i < length; i++)
        //{
        //    IColorTransform instance = _transforms[i];
        //    Type concreteType = instance.GetType();
        //    MethodInfo concreteMethod = concreteType.GetMethod(nameof(IColorTransform.Transform), types: [typeof(Vector4)]);
        //    Expression target = Expression.Constant(instance, concreteType);
        //    body = Expression.Call(target, concreteMethod, body);
        //}

        //var lambda = Expression.Lambda<PixelProcessorFunction>(body, inputParam);
        //return lambda.Compile();
    }
}

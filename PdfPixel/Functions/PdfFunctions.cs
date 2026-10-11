using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Functions;

/// <summary>
/// PDF function evaluator (subset of spec) used for color related operations (tint transforms, shadings, etc.).
/// </summary>
internal static class PdfFunctions
{
    /// <summary>
    /// Evaluate a dictionary that holds a /Function entry which may be a single function
    /// reference or an array of functions. Outputs are concatenated in order.
    /// Uses GetFunction to resolve each function object.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public static ReadOnlySpan<float> EvaluateColorFunctions(List<PdfFunction> functions, float input)
    {
        if (functions == null || functions.Count == 0)
        {
            return new ReadOnlySpan<float>();
        }

        if (functions.Count == 1)
        {
            return functions[0].Evaluate(input);
        }

        List<float> aggregate = new(functions.Count);

        foreach (PdfFunction function in functions)
        {
            ReadOnlySpan<float> part = function.Evaluate(input);
            if (!part.IsEmpty)
            {
                foreach (float item in part)
                {
                    aggregate.Add(item);
                }
            }
        }

        return aggregate.ToArray();
    }

    /// <summary>
    /// Evaluates one or more PDF color functions using the specified input values and returns the combined results as a
    /// read-only span of floats.
    /// </summary>
    /// <param name="functions">A list of <see cref="PdfFunction"/> instances to evaluate. If the list contains multiple functions, their
    /// results are concatenated in order. Can be null.</param>
    /// <param name="input">A read-only span of floating-point values to use as input for each function evaluation.</param>
    /// <returns>A read-only span of floats containing the combined results of evaluating each function in <paramref
    /// name="functions"/> with the specified <paramref name="input"/>. Returns an empty span if <paramref
    /// name="functions"/> is null.</returns>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public static ReadOnlySpan<float> EvaluateColorFunctions(List<PdfFunction> functions, in ReadOnlySpan<float> input)
    {
        if (functions == null || functions.Count == 0)
        {
            return new ReadOnlySpan<float>();
        }

        if (functions.Count == 1)
        {
            return functions[0].Evaluate(input);
        }

        List<float> aggregate = new(functions.Count);
        foreach (PdfFunction function in functions)
        {
            ReadOnlySpan<float> part = function.Evaluate(input);
            if (!part.IsEmpty)
            {
                foreach (float item in part)
                {
                    aggregate.Add(item);
                }
            }
        }

        return aggregate.ToArray();
    }

    /// <summary>
    /// Returns a parsed PdfFunction instance for the function held by <paramref name="key"/> of <paramref name="dictionary"/>, using the cache if available.
    /// </summary>
    /// <param name="dictionary">Dictionary holding the function entry.</param>
    /// <param name="key">Key of the function entry.</param>
    /// <returns>PdfFunction instance, or null if absent, unsupported or invalid.</returns>
    public static PdfFunction? GetFunction(PdfDictionary dictionary, in PdfString key)
        => dictionary.Document.ObjectCache.GetOrCreate(dictionary, key, FromObject);

    /// <summary>
    /// Returns a parsed PdfFunction instance for the function at <paramref name="index"/> of <paramref name="array"/>, using the cache if available.
    /// </summary>
    /// <param name="array">Array holding the function entry.</param>
    /// <param name="index">Index of the function entry.</param>
    /// <returns>PdfFunction instance, or null if absent, unsupported or invalid.</returns>
    public static PdfFunction? GetFunction(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    /// <summary>
    /// Parses a PdfFunction instance from the given function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>PdfFunction instance, or null if type is unsupported or invalid.</returns>
    public static PdfFunction? FromObject(PdfObject functionObject)
    {
        PdfFunctionType functionType = PdfFunction.GetFunctionType(functionObject);

        switch (functionType)
        {
            case PdfFunctionType.Sampled:
                {
                    return SampledPdfFunction.FromObject(functionObject);
                }
            case PdfFunctionType.Exponential:
                {
                    return ExponentialPdfFunction.FromObject(functionObject);
                }
            case PdfFunctionType.Stitching:
                {
                    return StitchingPdfFunction.FromObject(functionObject);
                }
            case PdfFunctionType.PostScript:
                {
                    return PostScriptPdfFunction.FromObject(functionObject);
                }
            default:
                return null;
        }
    }
}

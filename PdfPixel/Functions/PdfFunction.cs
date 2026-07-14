using System;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Functions;

/// <summary>
/// Abstract base class for all PDF function types. Provides evaluation interface and helpers.
/// </summary>
public abstract class PdfFunction
{
    /// <summary>
    /// Initializes the function with its input domain and optional output range.
    /// </summary>
    protected PdfFunction(PdfRange[] domain, PdfRange[]? range)
    {
        Domain = domain;
        Range = range;
    }

    /// <summary>
    /// Input domain, one entry per input dimension.
    /// </summary>
    public PdfRange[] Domain { get; }

    /// <summary>
    /// Optional output range, one entry per output component; <see langword="null"/> means unbounded.
    /// </summary>
    public PdfRange[]? Range { get; }

    /// <summary>
    /// Evaluate the function for a single input value.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>Evaluated output values.</returns>
    public abstract ReadOnlySpan<float> Evaluate(float value);

    /// <summary>
    /// Evaluate the function for multiple input values.
    /// </summary>
    /// <param name="values">Input values.</param>
    /// <returns>Evaluated output values.</returns>
    public abstract ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values);

    /// <summary>
    /// Gets the function type from the specified PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>Function type enum value.</returns>
    public static PdfFunctionType GetFunctionType(PdfObject functionObject)
    {
        if (functionObject == null || functionObject.Dictionary == null)
        {
            return PdfFunctionType.Unknown;
        }

        int typeValue = functionObject.Dictionary.GetIntegerOrDefault(PdfTokens.FunctionTypeKey);

        switch (typeValue)
        {
            case 0:
                return PdfFunctionType.Sampled;

            case 2:
                return PdfFunctionType.Exponential;

            case 3:
                return PdfFunctionType.Stitching;

            case 4:
                return PdfFunctionType.PostScript;

            default:
                return PdfFunctionType.Unknown;
        }
    }

    /// <summary>
    /// Clamps each value in the input span to the corresponding entry in <paramref name="range"/>.
    /// </summary>
    /// <param name="values">Input values to clamp.</param>
    /// <param name="range">One entry per component; no-op when <see langword="null"/>.</param>
    protected static void Clamp(in Span<float> values, PdfRange[]? range)
    {
        if (range == null)
        {
            return;
        }

        int count = Math.Min(values.Length, range.Length);
        for (int i = 0; i < count; i++)
        {
            values[i] = range[i].Clamp(values[i]);
        }
    }

    /// <summary>
    /// Clamps a single value to the entry at the specified index in <paramref name="range"/>.
    /// </summary>
    /// <param name="value">Value to clamp.</param>
    /// <param name="range">One entry per component; returns <paramref name="value"/> unchanged when <see langword="null"/> or too short.</param>
    /// <param name="index">Index of the entry to use.</param>
    /// <returns>Clamped value.</returns>
    protected static float Clamp(float value, PdfRange[]? range, int index)
    {
        if (range == null || range.Length <= index)
        {
            return value;
        }

        return range[index].Clamp(value);
    }

    /// <summary>
    /// Returns sampling points for the specified input dimension in the given domain range.
    /// Default implementation returns evenly spaced points using <paramref name="fallbackSamplesCount"/>.
    /// Derived types should override to provide type-specific sampling.
    /// </summary>
    /// <param name="dimension">Input dimension index.</param>
    /// <param name="domainStart">Domain start (inclusive).</param>
    /// <param name="domainEnd">Domain end (inclusive).</param>
    /// <param name="fallbackSamplesCount">Fallback number of samples.</param>
    /// <returns>Array of sampling coordinates.</returns>
    public virtual float[] GetSamplingPoints(int dimension, float domainStart, float domainEnd, int fallbackSamplesCount)
    {
        if (fallbackSamplesCount < 2)
        {
            fallbackSamplesCount = 2;
        }

        float start = domainStart;
        float end = domainEnd;
        var points = new float[fallbackSamplesCount];
        float countMinusOne = fallbackSamplesCount - 1;
        for (int i = 0; i < fallbackSamplesCount; i++)
        {
            float t = i / countMinusOne;
            points[i] = start + (t * (end - start));
        }

        return points;
    }
}

using System;
using PdfRender.Models;
using PdfRender.Text;

namespace PdfRender.Functions;

/// <summary>
/// Enumerates supported PDF function types.
/// </summary>
public enum PdfFunctionType
{
    Unknown = -1,
    Sampled = 0,
    Exponential = 2,
    Stitching = 3,
    PostScript = 4
}

/// <summary>
/// Abstract base class for all PDF function types. Provides evaluation interface and helpers.
/// </summary>
public abstract class PdfFunction
{
    public PdfFunction(float[] domain, float[] range)
    {
        Domain = domain;
        Range = range;
    }

    public float[] Domain { get; }

    public float[] Range { get; }

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
    /// Clamps each value in the input span to the corresponding min/max pair in the range span.
    /// </summary>
    /// <param name="values">Input values to clamp.</param>
    /// <param name="range">Range span, as [min0, max0, min1, max1, ...].</param>
    protected static void Clamp(Span<float> values, ReadOnlySpan<float> range)
    {
        if (range.IsEmpty)
        {
            return;
        }

        int count = Math.Min(values.Length, range.Length / 2);
        for (int i = 0; i < count; i++)
        {
            float min = range[i * 2];
            float max = range[i * 2 + 1];
            values[i] = Math.Max(min, Math.Min(max, values[i]));
        }
    }

    /// <summary>
    /// Clamps a single value to the min/max pair at the specified index in the range span.
    /// </summary>
    /// <param name="value">Value to clamp.</param>
    /// <param name="range">Range span, as [min0, max0, min1, max1, ...].</param>
    /// <param name="index">Index of the min/max pair to use.</param>
    /// <returns>Clamped value.</returns>
    protected static float Clamp(float value, ReadOnlySpan<float> range, int index)
    {
        if (range.Length < (index + 1) * 2)
        {
            return value;
        }

        float min = range[index * 2];
        float max = range[index * 2 + 1];
        return Math.Max(min, Math.Min(max, value));
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
        float[] points = new float[fallbackSamplesCount];
        float countMinusOne = fallbackSamplesCount - 1;
        for (int i = 0; i < fallbackSamplesCount; i++)
        {
            float t = i / countMinusOne;
            points[i] = start + t * (end - start);
        }
        return points;
    }
}

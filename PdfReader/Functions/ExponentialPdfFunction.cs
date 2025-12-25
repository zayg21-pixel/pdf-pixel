using System;
using System.Runtime.CompilerServices;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Functions;

/// <summary>
/// Represents a PDF exponential interpolation function (Type 2).
/// </summary>
public sealed class ExponentialPdfFunction : PdfFunction
{
    private const int LutSize = 1024;

    private readonly float[] _c0;
    private readonly float[] _c1;
    private readonly float _exponent;
    private readonly int _componentCount;
    private readonly float[] _buffer;

    // LUT optimization when range is defined
    private readonly float[] _lut;

    private ExponentialPdfFunction(float[] c0, float[] c1, float exponent, float[] domain, float[] range)
        : base(domain, range)
    {
        _c0 = c0;
        _c1 = c1;
        _exponent = exponent;
        _componentCount = Math.Min(c0.Length, c1.Length);
        _lut = BuildLut();
        _buffer = new float[_componentCount];
    }

    /// <summary>
    /// Builds a lookup table for exponential function evaluation with linear interpolation.
    /// </summary>
    private float[] BuildLut()
    {
        float[] lut = new float[LutSize * _componentCount];
        float lutSizeMinusOne = LutSize - 1;

        for (int i = 0; i < LutSize; i++)
        {
            // Normalize input to domain range [0, 1]
            float normalizedInput = i / lutSizeMinusOne;

            // Apply exponential function
            float xExp = _exponent <= 0f ? normalizedInput : MathF.Pow(normalizedInput, _exponent);

            for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
            {
                float result = _c0[componentIndex] + xExp * (_c1[componentIndex] - _c0[componentIndex]);

                // Apply range clamping during LUT build
                if (Range != null && Range.Length >= 2 * (componentIndex + 1))
                {
                    float rangeMin = Range[2 * componentIndex];
                    float rangeMax = Range[2 * componentIndex + 1];
#if NET5_0_OR_GREATER
                    result = Math.Clamp(result, rangeMin, rangeMax);
#else
                    result = result < rangeMin ? rangeMin : result > rangeMax ? rangeMax : result;
#endif
                }

                lut[i * _componentCount + componentIndex] = result;
            }
        }

        return lut;
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        float x = Clamp(value, Domain, 0);
        return EvaluateWithLut(x);
    }

    /// <summary>
    /// Evaluates the function using LUT with linear interpolation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<float> EvaluateWithLut(float x)
    {
        // Scale input to LUT index space
        float scaled = x * (LutSize - 1);
        int index = (int)scaled;
        float fraction = scaled - index;

        // Clamp index to valid range
        if (index >= LutSize - 1)
        {
            index = LutSize - 1;
            fraction = 0f;
        }

        int baseOffset = index * _componentCount;
        int nextOffset = (index + 1) * _componentCount;

        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            if (fraction == 0f || index == LutSize - 1)
            {
                // No interpolation needed
                _buffer[componentIndex] = _lut[baseOffset + componentIndex];
            }
            else
            {
                // Linear interpolation between adjacent LUT entries
                float value0 = _lut[baseOffset + componentIndex];
                float value1 = _lut[nextOffset + componentIndex];
#if NET8_0_OR_GREATER
                _buffer[componentIndex] = MathF.FusedMultiplyAdd(fraction, value1 - value0, value0);
#else
                _buffer[componentIndex] = value0 + fraction * (value1 - value0);
#endif
            }
        }

        return _buffer;
    }

    /// <summary>
    /// Evaluates the function using direct computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<float> EvaluateDirect(float x)
    {
        float xExp = _exponent <= 0f ? x : MathF.Pow(x, _exponent);

        float[] buffer = new float[_componentCount];
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            buffer[componentIndex] = _c0[componentIndex] + xExp * (_c1[componentIndex] - _c0[componentIndex]);
        }

        Clamp(buffer, Range);

        return buffer;
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
    {
        float x = values.Length > 0 ? values[0] : 0f;
        return Evaluate(x);
    }

    /// <summary>
    /// Creates an ExponentialPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>ExponentialPdfFunction instance, or null if invalid.</returns>
    public static ExponentialPdfFunction FromObject(PdfObject functionObject)
    {
        if (functionObject == null || functionObject.Dictionary == null)
        {
            return null;
        }

        float[] c0 = functionObject.Dictionary.GetArray(PdfTokens.C0Key)?.GetFloatArray();
        float[] c1 = functionObject.Dictionary.GetArray(PdfTokens.C1Key)?.GetFloatArray();
        float exponent = functionObject.Dictionary.GetFloatOrDefault(PdfTokens.FnNKey);
        float[] domain = functionObject.Dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        float[] range = functionObject.Dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();

        if (c0 == null || c0.Length == 0)
        {
            c0 = [0f];
        }
        if (c1 == null || c1.Length == 0)
        {
            c1 = [1f];
        }
        if (domain == null || domain.Length < 2)
        {
            domain = [0f, 1f];
        }

        int componentCount = Math.Min(c0.Length, c1.Length);
        if (componentCount == 0)
        {
            return null;
        }

        return new ExponentialPdfFunction(c0, c1, exponent, domain, range);
    }

    /// <summary>
    /// For exponential functions, return even points matching LUT size across the specified domain.
    /// </summary>
    public override float[] GetSamplingPoints(int dimension, float domainStart, float domainEnd, int fallbackSamplesCount = 256)
    {
        float start = domainStart;
        float end = domainEnd;
        int count = LutSize;
        float[] points = new float[count];
        float countMinusOne = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / countMinusOne;
            points[i] = start + t * (end - start);
        }
        return points;
    }
}

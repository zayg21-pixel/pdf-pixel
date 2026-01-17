using System;
using PdfRender.Models;
using PdfRender.Text;

namespace PdfRender.Functions;

/// <summary>
/// Represents a PDF exponential interpolation function (Type 2).
/// </summary>
public sealed class ExponentialPdfFunction : PdfFunction
{
    private readonly float[] _c0;
    private readonly float[] _cDifference;
    private readonly float _exponent;
    private readonly FastPowSeriesDegree3 _fastPow;
    private readonly int _componentCount;
    private readonly float[] _buffer;

    private ExponentialPdfFunction(float[] c0, float[] c1, float exponent, float[] domain, float[] range)
        : base(domain, range)
    {
        _c0 = c0;

        _componentCount = Math.Min(c0.Length, c1.Length);
        _cDifference = new float[_componentCount];

        for (int i = 0; i < _cDifference.Length; i++)
        {
            _cDifference[i] = c1[i] - c0[i];
        }

        _exponent = exponent;
        _fastPow = new FastPowSeriesDegree3(_exponent);
        _buffer = new float[_componentCount];
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        float x = Clamp(value, Domain, 0);

        float xExp = _fastPow.Evaluate(x);

        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            _buffer[componentIndex] = _c0[componentIndex] + xExp * _cDifference[componentIndex];
        }

        Clamp(_buffer, Range);

        return _buffer;
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
}

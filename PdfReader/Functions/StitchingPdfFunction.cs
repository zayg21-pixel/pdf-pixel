using System;
using System.Collections.Generic;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Functions;

/// <summary>
/// Represents a PDF stitching function (Type 3) that delegates to sub-functions based on bounds and encoding.
/// </summary>
public sealed class StitchingPdfFunction : PdfFunction
{
    private readonly List<PdfFunction> _subFunctions;
    private readonly float[] _bounds;
    private readonly float[] _encode;
    private readonly float[] _buffer;

    private StitchingPdfFunction(List<PdfFunction> subFunctions, float[] bounds, float[] encode, float[] domain, float[] range) : base(domain, range)
    {
        _subFunctions = subFunctions;
        _bounds = bounds;
        _encode = encode;

        if (Range != null && Range.Length >= 2)
        {
            int outputCount = Range.Length / 2;
            _buffer = new float[outputCount];
        }
    }

    /// <summary>
    /// Creates a StitchingPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>StitchingPdfFunction instance, or null if invalid.</returns>
    public static StitchingPdfFunction FromObject(PdfObject functionObject)
    {
        if (functionObject == null || functionObject.Dictionary == null)
        {
            return null;
        }

        var dictionary = functionObject.Dictionary;
        List<PdfObject> subFunctionObjects = dictionary.GetObjects(PdfTokens.FunctionsKey);
        if (subFunctionObjects == null || subFunctionObjects.Count == 0)
        {
            return null;
        }

        var subFunctions = new List<PdfFunction>(subFunctionObjects.Count);
        foreach (var subFunctionObject in subFunctionObjects)
        {
            PdfFunction subFunction = PdfFunctions.GetFunction(subFunctionObject);
            if (subFunction != null)
            {
                subFunctions.Add(subFunction);
            }
            else
            {
                // Add a placeholder empty function to preserve order
                subFunctions.Add(null);
            }
        }

        float[] bounds = dictionary.GetArray(PdfTokens.BoundsKey)?.GetFloatArray();
        float[] encode = dictionary.GetArray(PdfTokens.EncodeKey)?.GetFloatArray();
        float[] domain = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        float[] range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();

        return new StitchingPdfFunction(subFunctions, bounds, encode, domain, range);
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        float x = Clamp(value, Domain, 0);

        float domainStart = 0f;
        float domainEnd = 1f;
        if (Domain != null && Domain.Length >= 2)
        {
            domainStart = Domain[0];
            domainEnd = Domain[1];
        }

        int segmentIndex = 0;
        if (_bounds != null && _bounds.Length > 0)
        {
            while (segmentIndex < _bounds.Length && x >= _bounds[segmentIndex])
            {
                segmentIndex++;
            }

            if (segmentIndex >= _subFunctions.Count)
            {
                segmentIndex = _subFunctions.Count - 1;
            }
        }

        float mappedInput = x;
        if (_bounds != null && _encode != null && _encode.Length >= 2 * _subFunctions.Count)
        {
            float a = segmentIndex == 0 ? domainStart : _bounds[segmentIndex - 1];
            float b = segmentIndex < _bounds.Length ? _bounds[segmentIndex] : domainEnd;
            float e0 = _encode[2 * segmentIndex];
            float e1 = _encode[2 * segmentIndex + 1];
            float length = b - a;
            float localT = length != 0f ? (x - a) / length : 0f;
            mappedInput = e0 + localT * (e1 - e0);
        }

        PdfFunction childFunction = segmentIndex < _subFunctions.Count ? _subFunctions[segmentIndex] : null;
        if (childFunction != null)
        {
            ReadOnlySpan<float> childResult = childFunction.Evaluate(mappedInput);

            if (_buffer == null)
            {
                return childResult;
            }

            childResult.CopyTo(_buffer);

            Clamp(_buffer, Range);
            return _buffer;
        }

        return [];
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
    {
        float x = values.Length > 0 ? values[0] : 0f;
        return Evaluate(x);
    }

    /// <summary>
    /// For stitching functions, combine sampling points from each sub-function.
    /// Each sub-function is sampled in its Encode range and mapped back to the parent domain segment.
    /// </summary>
    public override float[] GetSamplingPoints(int dimension, float domainStart, float domainEnd, int fallbackSamplesCount)
    {
        float start = domainStart;
        float end = domainEnd;

        var points = new List<float>();

        int segmentCount = _subFunctions != null ? _subFunctions.Count : 0;
        if (segmentCount == 0)
        {
            return base.GetSamplingPoints(dimension, start, end, fallbackSamplesCount);
        }

        for (int seg = 0; seg < segmentCount; seg++)
        {
            float a = seg == 0 ? start : _bounds != null && _bounds.Length >= seg ? _bounds[seg - 1] : start;
            float b = _bounds != null && _bounds.Length > seg ? _bounds[seg] : end;

            PdfFunction child = _subFunctions[seg];
            if (child == null)
            {
                continue;
            }

            if (_encode != null && _encode.Length >= 2 * segmentCount)
            {
                float e0 = _encode[2 * seg];
                float e1 = _encode[2 * seg + 1];

                float[] childSamples = child.GetSamplingPoints(dimension, e0, e1, fallbackSamplesCount);
                float denom = e1 - e0;
                for (int i = 0; i < childSamples.Length; i++)
                {
                    float t = denom == 0f ? 0f : (childSamples[i] - e0) / denom;
                    float x = a + t * (b - a);
                    points.Add(x);
                }
            }
            else
            {
                float[] segSamples = base.GetSamplingPoints(dimension, a, b, fallbackSamplesCount);
                for (int i = 0; i < segSamples.Length; i++)
                {
                    points.Add(segSamples[i]);
                }
            }
        }

        // No clamping or sorting; duplicate or unordered positions are acceptable
        return points.ToArray();
    }
}

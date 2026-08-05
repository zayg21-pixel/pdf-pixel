using System;
using System.Collections.Generic;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Functions;

/// <summary>
/// Represents a PDF stitching function (Type 3) that delegates to sub-functions based on bounds and encoding.
/// </summary>
public sealed class StitchingPdfFunction : PdfFunction
{
    private readonly List<PdfFunction?> _subFunctions;
    private readonly float[]? _bounds;
    private readonly PdfRange[] _segmentDomains;
    private readonly PdfRange[]? _segmentEncodes;
    private readonly float[]? _buffer;

    private StitchingPdfFunction(List<PdfFunction?> subFunctions, float[]? bounds, PdfRange[]? encode, PdfRange[] domain, PdfRange[]? range)
        : base(domain, range)
    {
        _subFunctions = subFunctions;
        _bounds = bounds;

        int segmentCount = subFunctions.Count;
        _segmentDomains = BuildSegmentDomains(domain[0], bounds, segmentCount);
        _segmentEncodes = (encode != null && encode.Length >= segmentCount) ? encode : null;

        _buffer = (range != null) ? new float[range.Length] : null;
    }

    /// <summary>
    /// Builds one <see cref="PdfRange"/> per segment covering its [a, b) slice of <paramref name="domain"/>,
    /// using <paramref name="bounds"/> as the interior split points.
    /// </summary>
    private static PdfRange[] BuildSegmentDomains(in PdfRange domain, float[]? bounds, int segmentCount)
    {
        var segments = new PdfRange[segmentCount];
        for (int seg = 0; seg < segmentCount; seg++)
        {
            float lowerBound = domain.Min;
            if (seg > 0 && bounds != null && bounds.Length >= seg)
            {
                lowerBound = bounds[seg - 1];
            }

            float upperBound = domain.Max;
            if (bounds != null && bounds.Length > seg)
            {
                upperBound = bounds[seg];
            }

            segments[seg] = new PdfRange(lowerBound, upperBound);
        }

        return segments;
    }

    /// <summary>
    /// Creates a StitchingPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>StitchingPdfFunction instance, or null if invalid.</returns>
    public static StitchingPdfFunction? FromObject(PdfObject functionObject)
    {
        if (functionObject == null)
        {
            return null;
        }

        PdfDictionary dictionary = functionObject.Dictionary;
        List<PdfObject>? subFunctionObjects = dictionary.GetObjects(PdfTokens.FunctionsKey);
        if (subFunctionObjects == null || subFunctionObjects.Count == 0)
        {
            return null;
        }

        List<PdfFunction?> subFunctions = new(subFunctionObjects.Count);
        foreach (PdfObject subFunctionObject in subFunctionObjects)
        {
            PdfFunction? subFunction = PdfFunctions.GetFunction(subFunctionObject);
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

        float[]? bounds = dictionary.GetArray(PdfTokens.BoundsKey)?.GetFloatArray();
        float[]? encode = dictionary.GetArray(PdfTokens.EncodeKey)?.GetFloatArray();
        float[]? domain = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        float[]? range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();

        if (domain == null || domain.Length < 2)
        {
            domain = new float[] { 0f, 1f };
        }

        PdfRange[]? encodeEntries = (encode != null) ? PdfRange.FromArray(encode) : null;
        PdfRange[]? rangeEntries = (range != null) ? PdfRange.FromArray(range) : null;

        return new StitchingPdfFunction(subFunctions, bounds, encodeEntries, PdfRange.FromArray(domain), rangeEntries);
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        float x = Domain[0].Clamp(value);

        int segmentIndex = 0;
        if (_bounds?.Length > 0)
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
        if (_bounds != null && _segmentEncodes != null)
        {
            float localT = _segmentDomains[segmentIndex].Normalize(x);
            mappedInput = _segmentEncodes[segmentIndex].Denormalize(localT);
        }

        PdfFunction? childFunction = (segmentIndex < _subFunctions.Count) ? _subFunctions[segmentIndex] : null;
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

        return new ReadOnlySpan<float>();
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
    {
        float x = (values.Length > 0) ? values[0] : 0f;
        return Evaluate(x);
    }

    /// <summary>
    /// For stitching functions, combine sampling points from each sub-function.
    /// Each sub-function is sampled in its Encode range and mapped back to the parent domain segment.
    /// The combined result is sorted ascending.
    /// </summary>
    public override float[] GetSamplingPoints(int dimension, float domainStart, float domainEnd, int fallbackSamplesCount)
    {
        int segmentCount = _subFunctions?.Count ?? 0;
        if (segmentCount == 0)
        {
            return base.GetSamplingPoints(dimension, domainStart, domainEnd, fallbackSamplesCount);
        }

        PdfRange[] segmentDomains = BuildSegmentDomains(new PdfRange(domainStart, domainEnd), _bounds, segmentCount);

        List<float> points = [];
        for (int seg = 0; seg < segmentCount; seg++)
        {
            PdfFunction? child = _subFunctions?[seg];
            if (child == null)
            {
                continue;
            }

            PdfRange segmentDomain = segmentDomains[seg];
            if (_segmentEncodes != null)
            {
                PdfRange segmentEncode = _segmentEncodes[seg];
                float[] childSamples = child.GetSamplingPoints(dimension, segmentEncode.Min, segmentEncode.Max, fallbackSamplesCount);
                for (int i = 0; i < childSamples.Length; i++)
                {
                    float t = segmentEncode.Normalize(childSamples[i]);
                    points.Add(segmentDomain.Denormalize(t));
                }
            }
            else
            {
                float[] segmentSamples = base.GetSamplingPoints(dimension, segmentDomain.Min, segmentDomain.Max, fallbackSamplesCount);
                points.AddRange(segmentSamples);
            }
        }

        points.Sort();
        return points.ToArray();
    }
}

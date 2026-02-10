using System;
using System.Collections.Generic;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Text;

namespace PdfPixel.Functions;

/// <summary>
/// Represents a PDF sampled function (Type 0) supporting N-dimensional input with multilinear interpolation.
/// </summary>
public sealed class SampledPdfFunction : PdfFunction
{
    private readonly int[] _sizes;
    private readonly int _dimensions;
    private readonly int _componentCount;
    private readonly int[] _strides;
    private readonly float[] _table;
    private readonly float[] _decode;
    private readonly float[] _encode;

    private SampledPdfFunction(
        int[] sizes,
        int dimensions,
        int componentCount,
        int[] strides,
        float[] table,
        float[] range,
        float[] decode,
        float[] encode,
        float[] domain) : base(domain, range)
    {
        _sizes = sizes;
        _dimensions = dimensions;
        _componentCount = componentCount;
        _strides = strides;
        _table = table;
        _decode = decode;
        _encode = encode;
    }

    /// <summary>
    /// Creates a SampledPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>SampledPdfFunction instance, or null if invalid.</returns>
    public static SampledPdfFunction FromObject(PdfObject functionObject)
    {
        if (functionObject == null || functionObject.Dictionary == null)
        {
            return null;
        }

        var dictionary = functionObject.Dictionary;
        int[] sizeSource = dictionary.GetArray(PdfTokens.SizeKey)?.GetIntegerArray();
        if (sizeSource == null || sizeSource.Length == 0)
        {
            return null;
        }
        int dimensions = sizeSource.Length;

        float[] domain = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        if (domain == null || domain.Length < 2 * dimensions)
        {
            return null;
        }

        int[] sizes = new int[dimensions];
        for (int dimensionIndex = 0; dimensionIndex < dimensions; dimensionIndex++)
        {
            sizes[dimensionIndex] = Math.Max(1, sizeSource[dimensionIndex]);
        }

        int bitsPerSample = dictionary.GetIntegerOrDefault(PdfTokens.BitsPerSampleKey);
        if (bitsPerSample < 1 || bitsPerSample > 32)
        {
            return null;
        }

        float[] range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();
        if (range == null || range.Length < 2)
        {
            return null;
        }
        int componentCount = range.Length / 2;

        float[] encode = dictionary.GetArray(PdfTokens.EncodeKey)?.GetFloatArray();
        float[] decode = dictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();

        // Compute strides dimension 0 fastest
        int[] strides = new int[dimensions];
        int totalSamples = 1;
        for (int dimensionIndex = 0; dimensionIndex < dimensions; dimensionIndex++)
        {
            strides[dimensionIndex] = totalSamples;
            long nextTotal = (long)totalSamples * sizes[dimensionIndex];
            if (nextTotal > 8_000_000)
            {
                return null;
            }
            totalSamples = (int)nextTotal;
        }

        var raw = functionObject.DecodeAsMemory();
        if (raw.Length == 0)
        {
            return null;
        }

        var bitReader = new UintBitReader(raw.Span);
        float[] table = new float[totalSamples * componentCount];
        float factor = 1f / ((1UL << bitsPerSample) - 1);

        for (int linearIndex = 0; linearIndex < totalSamples; linearIndex++)
        {
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                uint sample = bitReader.ReadBits(bitsPerSample);
                float normalized = sample * factor;

                float outMin;
                float outMax;
                if (decode != null && decode.Length >= 2 * componentCount)
                {
                    outMin = decode[2 * componentIndex];
                    outMax = decode[2 * componentIndex + 1];
                }
                else
                {
                    outMin = range[2 * componentIndex];
                    outMax = range[2 * componentIndex + 1];
                }

                table[linearIndex * componentCount + componentIndex] = outMin + normalized * (outMax - outMin);
            }
        }

        return new SampledPdfFunction(
            sizes,
            dimensions,
            componentCount,
            strides,
            table,
            range,
            decode,
            encode,
            domain);
    }

    /// <summary>
    /// Gets the number of samples in each input dimension (sample grid sizes).
    /// </summary>
    public IReadOnlyList<int> Sizes => _sizes;

    /// <summary>
    /// Gets the number of input dimensions for this sampled function.
    /// </summary>
    public int Dimensions => _dimensions;

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        float[] input = new float[_dimensions];
        input[0] = value;
        return Evaluate(input);
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> inputs)
    {
        if (inputs.Length == 0)
        {
            return Array.Empty<float>();
        }

        int[] i0 = new int[_dimensions];
        int[] i1 = new int[_dimensions];
        float[] fractions = new float[_dimensions];

        for (int dimensionIndex = 0; dimensionIndex < _dimensions; dimensionIndex++)
        {
            float domainMin = Domain[2 * dimensionIndex];
            float domainMax = Domain[2 * dimensionIndex + 1];
            float inputValue = dimensionIndex < inputs.Length ? inputs[dimensionIndex] : 0f;
            // Clamp input to domain
            inputValue = Clamp(inputValue, Domain, dimensionIndex);

            float decodeMin = _decode != null && _decode.Length >= 2 * _dimensions
                ? _decode[2 * dimensionIndex]
                : domainMin;
            float decodeMax = _decode != null && _decode.Length >= 2 * _dimensions
                ? _decode[2 * dimensionIndex + 1]
                : domainMax;

            float domainT = (inputValue - domainMin) / (domainMax - domainMin);
            float mappedInput = decodeMin + domainT * (decodeMax - decodeMin);

            float encodeMin = 0f;
            float encodeMax = _sizes[dimensionIndex] - 1;
            if (_encode != null && _encode.Length >= 2 * _dimensions)
            {
                encodeMin = _encode[2 * dimensionIndex];
                encodeMax = _encode[2 * dimensionIndex + 1];
                if (Math.Abs(encodeMax - encodeMin) < 1e-12f)
                {
                    encodeMax = encodeMin + 1f;
                }
            }

            float u = encodeMin + (mappedInput - decodeMin) / (decodeMax - decodeMin) * (encodeMax - encodeMin);
            if (_sizes[dimensionIndex] == 1)
            {
                u = 0f;
            }
            if (u < 0f)
            {
                u = 0f;
            }
            else if (u > _sizes[dimensionIndex] - 1)
            {
                u = _sizes[dimensionIndex] - 1;
            }

            int floorIndex = (int)Math.Floor(u);
            int upperIndex = floorIndex + 1;
            if (upperIndex >= _sizes[dimensionIndex])
            {
                upperIndex = floorIndex;
            }

            i0[dimensionIndex] = floorIndex;
            i1[dimensionIndex] = upperIndex;
            fractions[dimensionIndex] = u - floorIndex;
        }

        float[] output = new float[_componentCount];
        int cornerCount = 1 << _dimensions;
        for (int corner = 0; corner < cornerCount; corner++)
        {
            float weight = 1f;
            int linearIndex = 0;
            for (int dimensionIndex = 0; dimensionIndex < _dimensions; dimensionIndex++)
            {
                bool useUpper = (corner & 1 << dimensionIndex) != 0;
                int sampleIndex = useUpper ? i1[dimensionIndex] : i0[dimensionIndex];
                float f = fractions[dimensionIndex];
                weight *= useUpper ? f : 1f - f;
                linearIndex += sampleIndex * _strides[dimensionIndex];
                if (weight == 0f)
                {
                    break;
                }
            }
            if (weight == 0f)
            {
                continue;
            }

            int baseOffset = linearIndex * _componentCount;
            for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
            {
                output[componentIndex] += weight * _table[baseOffset + componentIndex];
            }
        }

        // Clamp output to range
        Clamp(output, Range);

        return output;
    }

    /// <summary>
    /// For sampled functions, return sample grid coordinates for the requested dimension mapped to the domain.
    /// </summary>
    public override float[] GetSamplingPoints(int dimension, float domainStart, float domainEnd, int fallbackSamplesCount)
    {
        if (dimension < 0 || dimension >= _dimensions)
        {
            return base.GetSamplingPoints(dimension, domainStart, domainEnd, fallbackSamplesCount);
        }

        int size = _sizes[dimension];
        float start = Domain[2 * dimension];
        float end = Domain[2 * dimension + 1];

        // If encode specifies a custom range, respect it when mapping sample indices to domain
        float encodeMin = 0f;
        float encodeMax = size - 1;
        if (_encode != null && _encode.Length >= 2 * _dimensions)
        {
            encodeMin = _encode[2 * dimension];
            encodeMax = _encode[2 * dimension + 1];
            if (Math.Abs(encodeMax - encodeMin) < 1e-12f)
            {
                encodeMax = encodeMin + 1f;
            }
        }

        float[] points = new float[size];
        if (size == 1)
        {
            points[0] = start;
            return points;
        }

        for (int i = 0; i < size; i++)
        {
            float u = i;
            float t = (u - encodeMin) / (encodeMax - encodeMin);
            points[i] = start + t * (end - start);
        }

        return points;
    }
}

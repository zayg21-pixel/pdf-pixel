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
    private readonly int _componentCount;
    private readonly int[] _strides;
    private readonly float[] _table;
    private readonly float[]? _encode;
    private readonly int[] _lowerIndices;
    private readonly int[] _upperIndices;
    private readonly float[] _fractions;
    private readonly float[] _output;
    private readonly float[] _singleInput;

    private SampledPdfFunction(
        int[] sizes,
        int dimensions,
        int componentCount,
        int[] strides,
        float[] table,
        float[] range,
        float[]? encode,
        float[] domain)
        : base(domain, range)
    {
        _sizes = sizes;
        Dimensions = dimensions;
        _componentCount = componentCount;
        _strides = strides;
        _table = table;
        _encode = encode;

        _lowerIndices = new int[dimensions];
        _upperIndices = new int[dimensions];
        _fractions = new float[dimensions];
        _output = new float[componentCount];
        _singleInput = new float[1];
    }

    /// <summary>
    /// Creates a SampledPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>SampledPdfFunction instance, or null if invalid.</returns>
    public static SampledPdfFunction? FromObject(PdfObject functionObject)
    {
        if (functionObject == null)
        {
            return null;
        }

        PdfDictionary dictionary = functionObject.Dictionary;
        int[]? sizeSource = dictionary.GetArray(PdfTokens.SizeKey)?.GetIntegerArray();
        if (sizeSource == null || sizeSource.Length == 0)
        {
            return null;
        }

        int dimensions = sizeSource.Length;

        float[]? domain = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        if (domain == null || domain.Length < 2 * dimensions)
        {
            return null;
        }

        var sizes = new int[dimensions];
        for (int dimensionIndex = 0; dimensionIndex < dimensions; dimensionIndex++)
        {
            sizes[dimensionIndex] = Math.Max(1, sizeSource[dimensionIndex]);
        }

        int bitsPerSample = dictionary.GetIntegerOrDefault(PdfTokens.BitsPerSampleKey);
        if (bitsPerSample < 1 || bitsPerSample > 32)
        {
            return null;
        }

        float[]? range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();
        if (range == null || range.Length < 2)
        {
            return null;
        }

        int componentCount = range.Length / 2;

        float[]? encode = dictionary.GetArray(PdfTokens.EncodeKey)?.GetFloatArray();
        float[]? decode = dictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();

        // Compute strides dimension 0 fastest
        var strides = new int[dimensions];
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

        ReadOnlyMemory<byte> raw = functionObject.DecodeAsMemory();
        if (raw.Length == 0)
        {
            return null;
        }

        UintBitReaderFixedLength bitReader = new(raw.Span, bitsPerSample);
        var table = new float[totalSamples * componentCount];
        float factor = 1f / ((1UL << bitsPerSample) - 1);

        for (int linearIndex = 0; linearIndex < totalSamples; linearIndex++)
        {
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                uint sample = bitReader.Read();
                float normalized = sample * factor;

                float outMin;
                float outMax;
                if (decode != null && decode.Length >= 2 * componentCount)
                {
                    outMin = decode[2 * componentIndex];
                    outMax = decode[(2 * componentIndex) + 1];
                }
                else
                {
                    outMin = range[2 * componentIndex];
                    outMax = range[(2 * componentIndex) + 1];
                }

                table[(linearIndex * componentCount) + componentIndex] = outMin + (normalized * (outMax - outMin));
            }
        }

        return new SampledPdfFunction(
            sizes,
            dimensions,
            componentCount,
            strides,
            table,
            range,
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
    public int Dimensions { get; }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        _singleInput[0] = value;
        return Evaluate(_singleInput);
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
    {
        if (values.Length == 0)
        {
            return Array.Empty<float>();
        }

        for (int dimensionIndex = 0; dimensionIndex < Dimensions; dimensionIndex++)
        {
            int size = _sizes[dimensionIndex];
            float domainMin = Domain[2 * dimensionIndex];
            float domainMax = Domain[(2 * dimensionIndex) + 1];
            float inputValue = (dimensionIndex < values.Length) ? values[dimensionIndex] : 0f;
            // Clamp input to domain
            inputValue = Clamp(inputValue, Domain, dimensionIndex);

            float domainT = (inputValue - domainMin) / (domainMax - domainMin);

            float encodeMin = 0f;
            float encodeMax = size - 1;
            if (_encode != null && _encode.Length >= 2 * Dimensions)
            {
                encodeMin = _encode[2 * dimensionIndex];
                encodeMax = _encode[(2 * dimensionIndex) + 1];
                if (Math.Abs(encodeMax - encodeMin) < 1e-12f)
                {
                    encodeMax = encodeMin + 1f;
                }
            }

            float u = encodeMin + (domainT * (encodeMax - encodeMin));
            if (size == 1)
            {
                u = 0f;
            }

            if (u < 0f)
            {
                u = 0f;
            }
            else if (u > size - 1)
            {
                u = size - 1;
            }

            // Snap to the nearest grid index when u lands within float round-trip tolerance of it,
            // so evaluating the function's own sampling points reproduces the table row directly
            // instead of blending it with its neighbor.
            var roundedIndex = (int)Math.Round(u);
            int lowerIndex;
            int upperIndex;
            float fraction;
            if (roundedIndex >= 0 && roundedIndex < size && Math.Abs(u - roundedIndex) < 1e-4f)
            {
                lowerIndex = roundedIndex;
                upperIndex = roundedIndex;
                fraction = 0f;
            }
            else
            {
                lowerIndex = (int)Math.Floor(u);
                upperIndex = lowerIndex + 1;
                if (upperIndex >= size)
                {
                    upperIndex = lowerIndex;
                }

                fraction = u - lowerIndex;
            }

            _lowerIndices[dimensionIndex] = lowerIndex;
            _upperIndices[dimensionIndex] = upperIndex;
            _fractions[dimensionIndex] = fraction;
        }

        Array.Clear(_output, 0, _output.Length);
        int cornerCount = 1 << Dimensions;
        for (int corner = 0; corner < cornerCount; corner++)
        {
            float weight = 1f;
            int linearIndex = 0;
            for (int dimensionIndex = 0; dimensionIndex < Dimensions; dimensionIndex++)
            {
                bool useUpper = (corner & 1 << dimensionIndex) != 0;
                int sampleIndex = useUpper ? _upperIndices[dimensionIndex] : _lowerIndices[dimensionIndex];
                float f = _fractions[dimensionIndex];
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
                _output[componentIndex] += weight * _table[baseOffset + componentIndex];
            }
        }

        // Clamp output to range
        Clamp(_output, Range);

        return _output;
    }

    /// <summary>
    /// For sampled functions, return sample grid coordinates for the requested dimension mapped to the domain.
    /// </summary>
    public override float[] GetSamplingPoints(int dimension, float domainStart, float domainEnd, int fallbackSamplesCount)
    {
        if (dimension < 0 || dimension >= Dimensions)
        {
            return base.GetSamplingPoints(dimension, domainStart, domainEnd, fallbackSamplesCount);
        }

        int size = _sizes[dimension];
        float start = Domain[2 * dimension];
        float end = Domain[(2 * dimension) + 1];

        // If encode specifies a custom range, respect it when mapping sample indices to domain
        float encodeMin = 0f;
        float encodeMax = size - 1;
        if (_encode != null && _encode.Length >= 2 * Dimensions)
        {
            encodeMin = _encode[2 * dimension];
            encodeMax = _encode[(2 * dimension) + 1];
            if (Math.Abs(encodeMax - encodeMin) < 1e-12f)
            {
                encodeMax = encodeMin + 1f;
            }
        }

        var points = new float[size];
        if (size == 1)
        {
            points[0] = start;
            return points;
        }

        for (int i = 0; i < size; i++)
        {
            float u = i;
            float t = (u - encodeMin) / (encodeMax - encodeMin);
            points[i] = start + (t * (end - start));
        }

        return points;
    }
}

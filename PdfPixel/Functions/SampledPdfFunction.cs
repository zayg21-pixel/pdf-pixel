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
    private readonly PdfRange[] _encodeRanges;
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
        PdfRange[]? encode,
        PdfRange[] range,
        PdfRange[] domain)
        : base(domain, range)
    {
        _sizes = sizes;
        Dimensions = dimensions;
        _componentCount = componentCount;
        _strides = strides;
        _table = table;

        _encodeRanges = BuildEncodeRanges(encode, sizes, dimensions);

        _lowerIndices = new int[dimensions];
        _upperIndices = new int[dimensions];
        _fractions = new float[dimensions];
        _output = new float[componentCount];
        _singleInput = new float[1];
    }

    /// <summary>
    /// Builds one <see cref="PdfRange"/> per input dimension from the /Encode array, falling back to
    /// [0, size - 1] when unspecified.
    /// </summary>
    private static PdfRange[] BuildEncodeRanges(PdfRange[]? encode, int[] sizes, int dimensions)
    {
        if (encode != null && encode.Length >= dimensions)
        {
            return encode;
        }

        var ranges = new PdfRange[dimensions];
        for (int dimensionIndex = 0; dimensionIndex < dimensions; dimensionIndex++)
        {
            ranges[dimensionIndex] = new PdfRange(0f, sizes[dimensionIndex] - 1);
        }

        return ranges;
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

        PdfRange[] rangeEntries = PdfRange.FromArray(range);
        PdfRange[] outputRanges = (decode != null && decode.Length >= 2 * componentCount)
            ? PdfRange.FromArray(decode)
            : rangeEntries;

        UintBitReaderFixedLength bitReader = new(raw.Span, bitsPerSample);
        var table = new float[totalSamples * componentCount];
        float factor = 1f / ((1UL << bitsPerSample) - 1);

        for (int linearIndex = 0; linearIndex < totalSamples; linearIndex++)
        {
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                uint sample = bitReader.Read();
                float normalized = sample * factor;
                table[(linearIndex * componentCount) + componentIndex] = outputRanges[componentIndex].Denormalize(normalized);
            }
        }

        PdfRange[]? encodeEntries = (encode != null) ? PdfRange.FromArray(encode) : null;

        return new SampledPdfFunction(
            sizes,
            dimensions,
            componentCount,
            strides,
            table,
            encodeEntries,
            rangeEntries,
            PdfRange.FromArray(domain));
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
            PdfRange domainRange = Domain[dimensionIndex];
            float inputValue = (dimensionIndex < values.Length) ? values[dimensionIndex] : 0f;
            // Clamp input to domain
            inputValue = domainRange.Clamp(inputValue);

            float domainT = domainRange.Normalize(inputValue);
            float u = _encodeRanges[dimensionIndex].Denormalize(domainT);
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

            var lowerIndex = (int)Math.Floor(u);
            int upperIndex = lowerIndex + 1;
            if (upperIndex >= size)
            {
                upperIndex = lowerIndex;
            }

            float fraction = u - lowerIndex;

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
        PdfRange domainRange = Domain[dimension];

        var points = new float[size];
        if (size == 1)
        {
            points[0] = domainRange.Min;
            return points;
        }

        PdfRange encodeRange = _encodeRanges[dimension];
        for (int i = 0; i < size; i++)
        {
            float t = encodeRange.Normalize(i);
            points[i] = domainRange.Denormalize(t);
        }

        return points;
    }
}

using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
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
        private readonly float[] _rangePairs;
        private readonly float[] _decodePairs;
        private readonly float[] _encodePairs;
        private readonly float[] _domainPairs;

        private SampledPdfFunction(
            int[] sizes,
            int dimensions,
            int componentCount,
            int[] strides,
            float[] table,
            float[] rangePairs,
            float[] decodePairs,
            float[] encodePairs,
            float[] domainPairs)
        {
            _sizes = sizes;
            _dimensions = dimensions;
            _componentCount = componentCount;
            _strides = strides;
            _table = table;
            _rangePairs = rangePairs;
            _decodePairs = decodePairs;
            _encodePairs = encodePairs;
            _domainPairs = domainPairs;
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

            float[] domainPairs = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
            if (domainPairs == null || domainPairs.Length < 2 * dimensions)
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

            float[] rangePairs = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();
            if (rangePairs == null || rangePairs.Length < 2)
            {
                return null;
            }
            int componentCount = rangePairs.Length / 2;

            float[] encodePairs = dictionary.GetArray(PdfTokens.EncodeKey)?.GetFloatArray();
            float[] decodePairs = dictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();

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

            byte[] raw = functionObject.Document.StreamDecoder.DecodeContentStream(functionObject).ToArray();
            if (raw.Length == 0)
            {
                return null;
            }

            var bitReader = new FunctionBitReader(raw);
            float[] table = new float[totalSamples * componentCount];
            int maxSampleValue = (bitsPerSample == 32) ? -1 : ((1 << bitsPerSample) - 1);

            for (int linearIndex = 0; linearIndex < totalSamples; linearIndex++)
            {
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    uint sample = bitReader.ReadBits(bitsPerSample);
                    float normalized = bitsPerSample == 32
                        ? sample / 4294967295f
                        : (maxSampleValue > 0 ? sample / (float)maxSampleValue : 0f);

                    float outMin;
                    float outMax;
                    if (decodePairs != null && decodePairs.Length >= 2 * componentCount)
                    {
                        outMin = decodePairs[2 * componentIndex];
                        outMax = decodePairs[2 * componentIndex + 1];
                    }
                    else
                    {
                        outMin = rangePairs[2 * componentIndex];
                        outMax = rangePairs[2 * componentIndex + 1];
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
                rangePairs,
                decodePairs,
                encodePairs,
                domainPairs);
        }

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
                float domainMin = _domainPairs[2 * dimensionIndex];
                float domainMax = _domainPairs[2 * dimensionIndex + 1];
                if (Math.Abs(domainMax - domainMin) < 1e-12f)
                {
                    domainMax = domainMin + 1f;
                }

                float inputValue = inputs[dimensionIndex < inputs.Length ? dimensionIndex : 0];
                if (inputValue < domainMin)
                {
                    inputValue = domainMin;
                }
                else if (inputValue > domainMax)
                {
                    inputValue = domainMax;
                }

                float domainT = (inputValue - domainMin) / (domainMax - domainMin);

                float encodeMin = 0f;
                float encodeMax = _sizes[dimensionIndex] - 1;
                if (_encodePairs != null && _encodePairs.Length >= 2 * _dimensions)
                {
                    encodeMin = _encodePairs[2 * dimensionIndex];
                    encodeMax = _encodePairs[2 * dimensionIndex + 1];
                    if (Math.Abs(encodeMax - encodeMin) < 1e-12f)
                    {
                        encodeMax = encodeMin + 1f;
                    }
                }

                float u = encodeMin + domainT * (encodeMax - encodeMin);
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
                    bool useUpper = (corner & (1 << dimensionIndex)) != 0;
                    int sampleIndex = useUpper ? i1[dimensionIndex] : i0[dimensionIndex];
                    float f = fractions[dimensionIndex];
                    weight *= useUpper ? f : (1f - f);
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

            for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
            {
                float rangeMin = _rangePairs[2 * componentIndex];
                float rangeMax = _rangePairs[2 * componentIndex + 1];
                if (output[componentIndex] < rangeMin)
                {
                    output[componentIndex] = rangeMin;
                }
                else if (output[componentIndex] > rangeMax)
                {
                    output[componentIndex] = rangeMax;
                }
            }

            return output;
        }
    }
}

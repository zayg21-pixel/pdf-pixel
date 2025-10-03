using System;
using System.Collections.Generic;
using System.Linq;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// PDF function evaluator (subset of spec) used for color related operations (tint transforms, shadings, etc.).
    /// Supported function types:
    ///   0 - Sampled function (supports N-dimensional input, multilinear interpolation).
    ///   2 - Exponential interpolation (single input variable).
    ///   3 - Stitching function (single input variable, delegates to sub‑functions).
    /// Unsupported types (e.g. 4 PostScript calculator functions) produce an empty result.
    /// NOTE: Sample table ordering assumes that the FIRST input dimension varies fastest (dimension 0),
    /// so linear sample order increments dimension 0 first, then 1, etc. This matches internal producer expectations.
    /// </summary>
    internal static class PdfFunctions
    {
        /// <summary>
        /// Evaluate a dictionary that holds a /Function entry which may be a single function
        /// reference or an array of functions. If multiple functions are present the outputs
        /// are concatenated in order.
        /// </summary>
        public static float[] EvaluateColorFunctions(PdfDictionary container, float input)
        {
            if (container == null)
            {
                return Array.Empty<float>();
            }

            PdfObject singleFunction = container.GetPageObject(PdfTokens.FunctionKey);
            List<PdfObject> multipleFunctions = container.GetPageObjects(PdfTokens.FunctionKey);

            bool hasMultiple = multipleFunctions != null && multipleFunctions.Count > 1;

            if (singleFunction != null && singleFunction.Dictionary != null && !hasMultiple)
            {
                return EvaluateFunctionObject(singleFunction, input);
            }

            if (multipleFunctions != null && multipleFunctions.Count > 0)
            {
                var aggregate = new List<float>(multipleFunctions.Count * 4);
                foreach (PdfObject functionObject in multipleFunctions)
                {
                    if (functionObject == null)
                    {
                        continue;
                    }

                    if (functionObject.Dictionary == null)
                    {
                        continue;
                    }

                    float[] part = EvaluateFunctionObject(functionObject, input);
                    if (part != null && part.Length > 0)
                    {
                        aggregate.AddRange(part);
                    }
                }

                if (aggregate.Count == 0)
                {
                    return Array.Empty<float>();
                }

                return aggregate.ToArray();
            }

            return Array.Empty<float>();
        }

        /// <summary>
        /// Backwards compatible scalar input evaluator (wraps span overload).
        /// </summary>
        public static float[] EvaluateFunctionObject(PdfObject functionObject, float input)
        {
            float[] single = new float[1] { input };
            return EvaluateFunctionObject(functionObject, single);
        }

        /// <summary>
        /// General function dispatcher. Type 0 supports N inputs. Types 2 and 3 are limited to 1 input.
        /// </summary>
        public static float[] EvaluateFunctionObject(PdfObject functionObject, ReadOnlySpan<float> inputs)
        {
            if (functionObject == null || functionObject.Dictionary == null)
            {
                return Array.Empty<float>();
            }

            PdfDictionary dictionary = functionObject.Dictionary;
            int functionType = dictionary.GetInteger(PdfTokens.FunctionTypeKey);

            switch (functionType)
            {
                case 0:
                {
                    return EvaluateSampledFunction(functionObject, inputs);
                }
                case 2:
                {
                    float x = inputs.Length > 0 ? inputs[0] : 0f;
                    return EvaluateExponentialFunction(dictionary, x);
                }
                case 3:
                {
                    float s = inputs.Length > 0 ? inputs[0] : 0f;
                    return EvaluateStitchingFunction(functionObject, s);
                }
                default:
                {
                    return Array.Empty<float>();
                }
            }
        }

        /// <summary>
        /// Function Type 2: f(x) = C0 + x^N * (C1 - C0). Each component interpolated independently.
        /// </summary>
        private static float[] EvaluateExponentialFunction(PdfDictionary dictionary, float input)
        {
            List<IPdfValue> c0Array = dictionary.GetArray(PdfTokens.C0Key);
            List<IPdfValue> c1Array = dictionary.GetArray(PdfTokens.C1Key);
            float exponent = dictionary.GetFloat(PdfTokens.FnNKey);

            float[] c0 = c0Array != null ? ToFloatArray(c0Array) : new float[] { 0f };
            float[] c1 = c1Array != null ? ToFloatArray(c1Array) : new float[] { 1f };

            int componentCount = Math.Min(c0.Length, c1.Length);
            if (componentCount == 0)
            {
                return Array.Empty<float>();
            }

            float x = input;
            if (x < 0f)
            {
                x = 0f;
            }
            else if (x > 1f)
            {
                x = 1f;
            }

            float xExp = exponent <= 0f ? x : (float)Math.Pow(x, exponent);

            float[] output = new float[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                output[componentIndex] = c0[componentIndex] + xExp * (c1[componentIndex] - c0[componentIndex]);
            }

            return output;
        }

        /// <summary>
        /// Function Type 3: 1-D stitching function. Selects sub-function based on bounds and optional encoding.
        /// </summary>
        private static float[] EvaluateStitchingFunction(PdfObject functionObject, float input)
        {
            PdfDictionary dictionary = functionObject.Dictionary;
            List<PdfObject> subFunctions = dictionary.GetPageObjects(PdfTokens.FunctionsKey);
            if (subFunctions == null || subFunctions.Count == 0)
            {
                return Array.Empty<float>();
            }

            List<IPdfValue> boundsArray = dictionary.GetArray(PdfTokens.BoundsKey);
            List<IPdfValue> encodeArray = dictionary.GetArray(PdfTokens.EncodeKey);
            List<IPdfValue> domainArray = dictionary.GetArray(PdfTokens.DomainKey);

            float domainStart = 0f;
            float domainEnd = 1f;
            if (domainArray != null && domainArray.Count >= 2)
            {
                domainStart = domainArray[0].AsFloat();
                domainEnd = domainArray[1].AsFloat();
            }

            int segmentIndex = 0;
            float[] bounds = null;
            if (boundsArray != null && boundsArray.Count > 0)
            {
                bounds = new float[boundsArray.Count];
                for (int i = 0; i < boundsArray.Count; i++)
                {
                    bounds[i] = boundsArray[i].AsFloat();
                }

                while (segmentIndex < bounds.Length && input > bounds[segmentIndex])
                {
                    segmentIndex++;
                }

                if (segmentIndex >= subFunctions.Count)
                {
                    segmentIndex = subFunctions.Count - 1;
                }
            }

            float mappedInput = input;
            if (bounds != null && encodeArray != null && encodeArray.Count >= 2 * subFunctions.Count)
            {
                float a = segmentIndex == 0 ? domainStart : bounds[segmentIndex - 1];
                float b = segmentIndex < bounds.Length ? bounds[segmentIndex] : domainEnd;
                float e0 = encodeArray[2 * segmentIndex].AsFloat();
                float e1 = encodeArray[2 * segmentIndex + 1].AsFloat();
                float length = b - a;
                float localT = length != 0f ? (input - a) / length : 0f;
                mappedInput = e0 + localT * (e1 - e0);
            }

            return EvaluateFunctionObject(subFunctions[segmentIndex], mappedInput);
        }

        /// <summary>
        /// Function Type 0: Sampled function supporting N-dimensional input with multilinear interpolation.
        /// </summary>
        private static float[] EvaluateSampledFunction(PdfObject functionObject, ReadOnlySpan<float> inputs)
        {
            try
            {
                PdfDictionary dictionary = functionObject.Dictionary;
                if (dictionary == null)
                {
                    return Array.Empty<float>();
                }

                PdfDocument document = dictionary.Document;

                List<IPdfValue> sizeArray = dictionary.GetArray(PdfTokens.SizeKey);
                if (sizeArray == null || sizeArray.Count == 0)
                {
                    return Array.Empty<float>();
                }
                int dimensions = sizeArray.Count;
                if (inputs.Length == 0)
                {
                    return Array.Empty<float>();
                }

                List<IPdfValue> domainArray = dictionary.GetArray(PdfTokens.DomainKey);
                if (domainArray == null || domainArray.Count < 2 * dimensions)
                {
                    return Array.Empty<float>();
                }

                // Attempt cache retrieval (only for referenced objects since inline dictionaries would reuse same reference of 0,0 otherwise)
                PdfFunctionCacheEntry cacheEntry = null;
                if (functionObject.Reference.IsValid && document != null)
                {
                    document.FunctionCache.TryGetValue(functionObject.Reference, out cacheEntry);
                }

                int[] sizes;
                int componentCount;
                int[] strides;
                float[] table;
                int bitsPerSample;
                float[] rangePairs;
                float[] decodePairs;
                float[] encodePairs;

                if (cacheEntry != null)
                {
                    sizes = cacheEntry.Sizes;
                    componentCount = cacheEntry.ComponentCount;
                    strides = cacheEntry.Strides;
                    table = cacheEntry.Table;
                    bitsPerSample = cacheEntry.BitsPerSample;
                    rangePairs = cacheEntry.RangePairs;
                    decodePairs = cacheEntry.DecodePairs;
                    encodePairs = cacheEntry.EncodePairs;
                }
                else
                {
                    sizes = new int[dimensions];
                    for (int d = 0; d < dimensions; d++)
                    {
                        sizes[d] = Math.Max(1, sizeArray[d].AsInteger());
                    }

                    bitsPerSample = dictionary.GetInteger(PdfTokens.BitsPerSampleKey);
                    if (bitsPerSample < 1 || bitsPerSample > 32)
                    {
                        return Array.Empty<float>();
                    }

                    List<IPdfValue> rangeArray = dictionary.GetArray(PdfTokens.RangeKey);
                    if (rangeArray == null || rangeArray.Count < 2)
                    {
                        return Array.Empty<float>();
                    }
                    componentCount = rangeArray.Count / 2;

                    List<IPdfValue> encodeArray = dictionary.GetArray(PdfTokens.EncodeKey);
                    List<IPdfValue> decodeArray = dictionary.GetArray(PdfTokens.DecodeKey);

                    encodePairs = encodeArray != null ? ToFloatArray(encodeArray).ToArray() : null;
                    decodePairs = decodeArray != null ? ToFloatArray(decodeArray).ToArray() : null;

                    rangePairs = ToFloatArray(rangeArray).ToArray();

                    // Compute strides dimension 0 fastest
                    strides = new int[dimensions];
                    int totalSamples = 1;
                    for (int d = 0; d < dimensions; d++)
                    {
                        strides[d] = totalSamples;
                        long nextTotal = (long)totalSamples * sizes[d];
                        if (nextTotal > 8_000_000)
                        {
                            return Array.Empty<float>();
                        }
                        totalSamples = (int)nextTotal;
                    }

                    byte[] raw = PdfStreamDecoder.DecodeContentStream(functionObject).ToArray();
                    if (raw.Length == 0)
                    {
                        return Array.Empty<float>();
                    }

                    var bitReader = new BitReader(raw);
                    table = new float[totalSamples * componentCount];
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

                    if (functionObject.Reference.IsValid && document != null)
                    {
                        document.FunctionCache[functionObject.Reference] = new PdfFunctionCacheEntry(
                            sizes,
                            componentCount,
                            bitsPerSample,
                            table,
                            strides,
                            rangePairs,
                            decodePairs,
                            encodePairs);
                    }
                }

                // Interpolation mapping
                int[] i0 = new int[dimensions];
                int[] i1 = new int[dimensions];
                float[] fractions = new float[dimensions];

                for (int dimensionIndex = 0; dimensionIndex < dimensions; dimensionIndex++)
                {
                    float domainMin = domainArray[2 * dimensionIndex].AsFloat();
                    float domainMax = domainArray[2 * dimensionIndex + 1].AsFloat();
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
                    float encodeMax = sizes[dimensionIndex] - 1;
                    if (encodePairs != null && encodePairs.Length >= 2 * dimensions)
                    {
                        encodeMin = encodePairs[2 * dimensionIndex];
                        encodeMax = encodePairs[2 * dimensionIndex + 1];
                        if (Math.Abs(encodeMax - encodeMin) < 1e-12f)
                        {
                            encodeMax = encodeMin + 1f;
                        }
                    }

                    float u = encodeMin + domainT * (encodeMax - encodeMin);
                    if (sizes[dimensionIndex] == 1)
                    {
                        u = 0f;
                    }
                    if (u < 0f)
                    {
                        u = 0f;
                    }
                    else if (u > sizes[dimensionIndex] - 1)
                    {
                        u = sizes[dimensionIndex] - 1;
                    }

                    int floorIndex = (int)Math.Floor(u);
                    int upperIndex = floorIndex + 1;
                    if (upperIndex >= sizes[dimensionIndex])
                    {
                        upperIndex = floorIndex;
                    }

                    i0[dimensionIndex] = floorIndex;
                    i1[dimensionIndex] = upperIndex;
                    fractions[dimensionIndex] = u - floorIndex;
                }

                float[] output = new float[componentCount];
                int cornerCount = 1 << dimensions;
                for (int corner = 0; corner < cornerCount; corner++)
                {
                    float weight = 1f;
                    int linearIndex = 0;
                    for (int dimensionIndex = 0; dimensionIndex < dimensions; dimensionIndex++)
                    {
                        bool useUpper = (corner & (1 << dimensionIndex)) != 0;
                        int sampleIndex = useUpper ? i1[dimensionIndex] : i0[dimensionIndex];
                        float f = fractions[dimensionIndex];
                        weight *= useUpper ? f : (1f - f);
                        linearIndex += sampleIndex * strides[dimensionIndex];
                        if (weight == 0f)
                        {
                            break;
                        }
                    }
                    if (weight == 0f)
                    {
                        continue;
                    }

                    int baseOffset = linearIndex * componentCount;
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        output[componentIndex] += weight * table[baseOffset + componentIndex];
                    }
                }

                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    float rangeMin = rangePairs[2 * componentIndex];
                    float rangeMax = rangePairs[2 * componentIndex + 1];
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
            catch
            {
                return Array.Empty<float>();
            }
        }

        private static float[] ToFloatArray(List<IPdfValue> array)
        {
            if (array == null || array.Count == 0)
            {
                return Array.Empty<float>();
            }

            float[] result = new float[array.Count];
            for (int index = 0; index < array.Count; index++)
            {
                result[index] = array[index].AsFloat();
            }
            return result;
        }

        private sealed class BitReader
        {
            private readonly byte[] _data;
            private int _bitPosition;

            public BitReader(byte[] data)
            {
                _data = data ?? Array.Empty<byte>();
                _bitPosition = 0;
            }

            public uint ReadBits(int count)
            {
                if (count <= 0 || count > 32)
                {
                    return 0;
                }

                uint value = 0;
                for (int bitIndex = 0; bitIndex < count; bitIndex++)
                {
                    int byteIndex = _bitPosition >> 3;
                    if (byteIndex >= _data.Length)
                    {
                        break;
                    }

                    int shift = 7 - (_bitPosition & 7);
                    uint bit = (uint)((_data[byteIndex] >> shift) & 1);
                    value = (value << 1) | bit;
                    _bitPosition++;
                }
                return value;
            }
        }
    }
}

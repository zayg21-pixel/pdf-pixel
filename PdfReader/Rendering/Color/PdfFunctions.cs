using System;
using System.Collections.Generic;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Evaluator for simple PDF functions (types 0, 2, 3) used in shadings, tint transforms, etc.
    /// - Type 0: Sampled function (only 1D input supported here)
    /// - Type 2: Exponential interpolation
    /// - Type 3: Stitching (1D) function
    /// PostScript (Type 4) and other types are intentionally not implemented in this minimal renderer.
    /// </summary>
    internal static class PdfFunctions
    {
        /// <summary>
        /// Evaluate a /Function entry that can be a single function dictionary or an array of functions.
        /// If an array, concatenates the outputs of each function in order.
        /// </summary>
        public static float[] EvaluateColorFunctions(PdfDictionary container, float input)
        {
            if (container == null)
            {
                return Array.Empty<float>();
            }

            var single = container.GetPageObject(PdfTokens.FunctionKey);
            var multiple = container.GetPageObjects(PdfTokens.FunctionKey);

            // Single function case
            if (single != null && single.Dictionary != null && (multiple == null || multiple.Count <= 1))
            {
                return EvaluateFunctionObject(single, input);
            }

            // Multiple functions case
            if (multiple != null && multiple.Count > 0)
            {
                var aggregate = new List<float>(multiple.Count * 4);
                foreach (var funcObj in multiple)
                {
                    if (funcObj == null)
                    {
                        continue;
                    }

                    if (funcObj.Dictionary == null)
                    {
                        continue;
                    }

                    var part = EvaluateFunctionObject(funcObj, input);
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
        /// Evaluate a single function object (types 0, 2, 3). Unsupported types return an empty array.
        /// </summary>
        public static float[] EvaluateFunctionObject(PdfObject functionObject, float input)
        {
            if (functionObject == null)
            {
                return Array.Empty<float>();
            }

            var dict = functionObject.Dictionary;
            if (dict == null)
            {
                return Array.Empty<float>();
            }

            int functionType = dict.GetInteger(PdfTokens.FunctionTypeKey);
            switch (functionType)
            {
                case 0:
                    return EvaluateSampledFunction1D(functionObject, input);
                case 2:
                    return EvaluateExponentialFunction(dict, input);
                case 3:
                    return EvaluateStitchingFunction(functionObject, input);
                default:
                    return Array.Empty<float>();
            }
        }

        /// <summary>
        /// Function Type 2: Exponential interpolation: f(x) = C0 + x^N * (C1 - C0)
        /// Components are interpolated independently; x is clamped to [0,1].
        /// </summary>
        private static float[] EvaluateExponentialFunction(PdfDictionary dict, float input)
        {
            var c0Array = dict.GetArray(PdfTokens.C0Key);
            var c1Array = dict.GetArray(PdfTokens.C1Key);
            float exponent = dict.GetFloat(PdfTokens.FnNKey);

            var c0 = c0Array != null ? ToFloatArray(c0Array) : new float[] { 0f };
            var c1 = c1Array != null ? ToFloatArray(c1Array) : new float[] { 1f };

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

            var output = new float[componentCount];
            for (int i = 0; i < componentCount; i++)
            {
                output[i] = c0[i] + xExp * (c1[i] - c0[i]);
            }

            return output;
        }

        /// <summary>
        /// Function Type 3: Stitching function (1D).
        /// Selects a sub-function according to bounds and optionally re-encodes the input value.
        /// </summary>
        private static float[] EvaluateStitchingFunction(PdfObject functionObject, float input)
        {
            var dict = functionObject.Dictionary;
            var subFunctions = dict.GetPageObjects(PdfTokens.FunctionsKey);
            if (subFunctions == null || subFunctions.Count == 0)
            {
                return Array.Empty<float>();
            }

            var boundsArray = dict.GetArray(PdfTokens.BoundsKey);
            var encodeArray = dict.GetArray(PdfTokens.EncodeKey);
            var domainArray = dict.GetArray(PdfTokens.DomainKey);

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
        /// Function Type 0: Sampled function (only 1D input supported here). Multi-input variants are ignored.
        /// </summary>
        private static float[] EvaluateSampledFunction1D(PdfObject functionObject, float input)
        {
            try
            {
                var dict = functionObject.Dictionary;
                if (dict == null)
                {
                    return Array.Empty<float>();
                }

                // Domain
                var domainArray = dict.GetArray(PdfTokens.DomainKey);
                if (domainArray == null || domainArray.Count < 2)
                {
                    return Array.Empty<float>();
                }
                float domainStart = domainArray[0].AsFloat();
                float domainEnd = domainArray[1].AsFloat();
                if (Math.Abs(domainEnd - domainStart) < 1e-12f)
                {
                    domainEnd = domainStart + 1f;
                }

                // Size (first dimension only for 1D)
                var sizeArray = dict.GetArray(PdfTokens.SizeKey);
                if (sizeArray == null || sizeArray.Count < 1)
                {
                    return Array.Empty<float>();
                }
                int sampleCount = Math.Max(1, sizeArray[0].AsInteger());

                // Bits per sample
                int bitsPerSample = dict.GetInteger(PdfTokens.BitsPerSampleKey);
                if (bitsPerSample <= 0)
                {
                    // Defensive: fallback literal key if malformed
                    bitsPerSample = dict.GetInteger("/BitsPerSample");
                }
                if (bitsPerSample < 1 || bitsPerSample > 32)
                {
                    return Array.Empty<float>();
                }

                // Range array (required) -> pairs per component
                var rangeArray = dict.GetArray(PdfTokens.RangeKey);
                if (rangeArray == null || rangeArray.Count < 2)
                {
                    return Array.Empty<float>();
                }
                int componentCount = rangeArray.Count / 2;

                // Encode (optional)
                var encodeArray = dict.GetArray(PdfTokens.EncodeKey);
                float encodeStart = 0f;
                float encodeEnd = sampleCount - 1;
                if (encodeArray != null && encodeArray.Count >= 2)
                {
                    encodeStart = encodeArray[0].AsFloat();
                    encodeEnd = encodeArray[1].AsFloat();
                    if (Math.Abs(encodeEnd - encodeStart) < 1e-12f)
                    {
                        encodeEnd = encodeStart + 1f;
                    }
                }

                // Decode (optional override for sample expansion)
                var decodeArray = dict.GetArray(PdfTokens.DecodeKey);

                // Normalize input
                float clamped = input;
                if (clamped < domainStart)
                {
                    clamped = domainStart;
                }
                else if (clamped > domainEnd)
                {
                    clamped = domainEnd;
                }
                float domainT = (clamped - domainStart) / (domainEnd - domainStart);

                // Map to sample index space
                float u = encodeStart + domainT * (encodeEnd - encodeStart);
                if (sampleCount == 1)
                {
                    u = 0f;
                }
                if (u < 0f)
                {
                    u = 0f;
                }
                else if (u > sampleCount - 1)
                {
                    u = sampleCount - 1;
                }
                int index0 = (int)Math.Floor(u);
                int index1 = index0 + 1;
                if (index1 >= sampleCount)
                {
                    index1 = index0;
                }
                float frac = u - index0;

                // Read raw samples
                var raw = PdfStreamDecoder.DecodeContentStream(functionObject).ToArray();
                if (raw.Length == 0)
                {
                    return Array.Empty<float>();
                }

                var bitReader = new BitReader(raw);
                float[,] table = new float[sampleCount, componentCount];
                int maxSampleValue = (bitsPerSample == 32) ? -1 : ((1 << bitsPerSample) - 1);

                for (int s = 0; s < sampleCount; s++)
                {
                    for (int c = 0; c < componentCount; c++)
                    {
                        uint sample = bitReader.ReadBits(bitsPerSample);
                        float normalized;
                        if (bitsPerSample == 32)
                        {
                            normalized = sample / 4294967295f;
                        }
                        else
                        {
                            normalized = maxSampleValue > 0 ? sample / (float)maxSampleValue : 0f;
                        }

                        float outMin;
                        float outMax;
                        if (decodeArray != null && decodeArray.Count >= 2 * componentCount)
                        {
                            outMin = decodeArray[2 * c].AsFloat();
                            outMax = decodeArray[2 * c + 1].AsFloat();
                        }
                        else
                        {
                            outMin = rangeArray[2 * c].AsFloat();
                            outMax = rangeArray[2 * c + 1].AsFloat();
                        }

                        table[s, c] = outMin + normalized * (outMax - outMin);
                    }
                }

                var output = new float[componentCount];
                for (int c = 0; c < componentCount; c++)
                {
                    float v0 = table[index0, c];
                    float v1 = table[index1, c];
                    float value;
                    if (index0 == index1)
                    {
                        value = v0;
                    }
                    else
                    {
                        value = v0 + frac * (v1 - v0);
                    }

                    float rMin = rangeArray[2 * c].AsFloat();
                    float rMax = rangeArray[2 * c + 1].AsFloat();
                    if (value < rMin)
                    {
                        value = rMin;
                    }
                    else if (value > rMax)
                    {
                        value = rMax;
                    }

                    output[c] = value;
                }

                return output;
            }
            catch
            {
                return Array.Empty<float>();
            }
        }

        /// <summary>
        /// Convert a PDF value array to float[] (utility helper).
        /// </summary>
        private static float[] ToFloatArray(List<IPdfValue> array)
        {
            var result = new float[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                result[i] = array[i].AsFloat();
            }
            return result;
        }

        /// <summary>
        /// Bit-level reader for function sample tables.
        /// </summary>
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
                for (int i = 0; i < count; i++)
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

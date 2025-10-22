using System;
using System.Collections.Generic;
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
        /// reference or an array of functions. Outputs are concatenated in order.
        /// Uses GetFunction to resolve each function object.
        /// </summary>
        public static ReadOnlySpan<float> EvaluateColorFunctions(List<PdfFunction> functions, float input)
        {
            if (functions == null)
            {
                return Array.Empty<float>();
            }

            var aggregate = new List<float>(functions.Count * 4);

            foreach (PdfFunction function in functions)
            {
                ReadOnlySpan<float> part = function.Evaluate(input);
                if (part != null && part.Length > 0)
                {
                    foreach (float item in part)
                    {
                        aggregate.Add(item);
                    }
                }
            }

            return aggregate.ToArray();
        }

        /// <summary>
        /// Returns a parsed PdfFunction instance for the given function object, using the cache if available.
        /// </summary>
        /// <param name="functionObject">PDF function object.</param>
        /// <returns>PdfFunction instance, or null if type is unsupported or invalid.</returns>
        public static PdfFunction GetFunction(PdfObject functionObject)
        {
            if (functionObject == null || functionObject.Dictionary == null)
            {
                return null;
            }

            if (functionObject.Reference.IsValid && functionObject.Document != null)
            {
                var cache = functionObject.Document.FunctionObjectCache;
                if (cache.TryGetValue(functionObject.Reference, out PdfFunction cachedFunction))
                {
                    return cachedFunction;
                }
            }

            PdfFunctionType functionType = PdfFunction.GetFunctionType(functionObject);
            PdfFunction function = null;
            switch (functionType)
            {
                case PdfFunctionType.Sampled:
                {
                    function = SampledPdfFunction.FromObject(functionObject);
                    break;
                }
                case PdfFunctionType.Exponential:
                {
                    function = ExponentialPdfFunction.FromObject(functionObject);
                    break;
                }
                case PdfFunctionType.Stitching:
                {
                    function = StitchingPdfFunction.FromObject(functionObject);
                    break;
                }
                default:
                {
                    // Unsupported or unknown function type
                    return null;
                }
            }

            if (function != null && functionObject.Reference.IsValid && functionObject.Document != null)
            {
                var cache = functionObject.Document.FunctionObjectCache;
                cache[functionObject.Reference] = function;
            }

            return function;
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

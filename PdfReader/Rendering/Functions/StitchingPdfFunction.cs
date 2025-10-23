using System;
using System.Collections.Generic;
using PdfReader.Models;

namespace PdfReader.Rendering.Functions
{
    /// <summary>
    /// Represents a PDF stitching function (Type 3) that delegates to sub-functions based on bounds and encoding.
    /// </summary>
    public sealed class StitchingPdfFunction : PdfFunction
    {
        private readonly List<PdfFunction> _subFunctions;
        private readonly float[] _bounds;
        private readonly float[] _encode;
        private readonly float[] _domain;

        private StitchingPdfFunction(List<PdfFunction> subFunctions, float[] bounds, float[] encode, float[] domain)
        {
            _subFunctions = subFunctions;
            _bounds = bounds;
            _encode = encode;
            _domain = domain;
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
            List<PdfObject> subFunctionObjects = dictionary.GetPageObjects(PdfTokens.FunctionsKey);
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

            return new StitchingPdfFunction(subFunctions, bounds, encode, domain);
        }

        /// <inheritdoc />
        public override ReadOnlySpan<float> Evaluate(float value)
        {
            float domainStart = 0f;
            float domainEnd = 1f;
            if (_domain != null && _domain.Length >= 2)
            {
                domainStart = _domain[0];
                domainEnd = _domain[1];
            }

            int segmentIndex = 0;
            if (_bounds != null && _bounds.Length > 0)
            {
                while (segmentIndex < _bounds.Length && value > _bounds[segmentIndex])
                {
                    segmentIndex++;
                }

                if (segmentIndex >= _subFunctions.Count)
                {
                    segmentIndex = _subFunctions.Count - 1;
                }
            }

            float mappedInput = value;
            if (_bounds != null && _encode != null && _encode.Length >= 2 * _subFunctions.Count)
            {
                float a = segmentIndex == 0 ? domainStart : _bounds[segmentIndex - 1];
                float b = segmentIndex < _bounds.Length ? _bounds[segmentIndex] : domainEnd;
                float e0 = _encode[2 * segmentIndex];
                float e1 = _encode[2 * segmentIndex + 1];
                float length = b - a;
                float localT = length != 0f ? (value - a) / length : 0f;
                mappedInput = e0 + localT * (e1 - e0);
            }

            PdfFunction childFunction = segmentIndex < _subFunctions.Count ? _subFunctions[segmentIndex] : null;
            if (childFunction != null)
            {
                return childFunction.Evaluate(mappedInput);
            }
            return Array.Empty<float>();
        }

        /// <inheritdoc />
        public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
        {
            float x = values.Length > 0 ? values[0] : 0f;
            return Evaluate(x);
        }
    }
}

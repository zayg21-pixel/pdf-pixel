using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Represents a PDF exponential interpolation function (Type 2).
    /// </summary>
    public sealed class ExponentialPdfFunction : PdfFunction
    {
        private readonly float[] _c0;
        private readonly float[] _c1;
        private readonly float _exponent;
        private readonly int _componentCount;

        private ExponentialPdfFunction(float[] c0, float[] c1, float exponent)
        {
            _c0 = c0;
            _c1 = c1;
            _exponent = exponent;
            _componentCount = Math.Min(c0.Length, c1.Length);
        }

        /// <inheritdoc />
        public override ReadOnlySpan<float> Evaluate(float value)
        {
            float x = value;
            if (x < 0f)
            {
                x = 0f;
            }
            else if (x > 1f)
            {
                x = 1f;
            }

            float xExp = _exponent <= 0f ? x : (float)Math.Pow(x, _exponent);

            float[] buffer = new float[_componentCount];
            for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
            {
                buffer[componentIndex] = _c0[componentIndex] + xExp * (_c1[componentIndex] - _c0[componentIndex]);
            }

            return buffer;
        }

        /// <inheritdoc />
        public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
        {
            float x = values.Length > 0 ? values[0] : 0f;
            return Evaluate(x);
        }


        /// <summary>
        /// Creates an ExponentialPdfFunction from a PDF function object.
        /// </summary>
        /// <param name="functionObject">PDF function object.</param>
        /// <returns>ExponentialPdfFunction instance, or null if invalid.</returns>
        public static ExponentialPdfFunction FromObject(PdfObject functionObject)
        {
            if (functionObject == null || functionObject.Dictionary == null)
            {
                return null;
            }

            float[] c0 = functionObject.Dictionary.GetArray(PdfTokens.C0Key)?.GetFloatArray();
            float[] c1 = functionObject.Dictionary.GetArray(PdfTokens.C1Key)?.GetFloatArray();
            float exponent = functionObject.Dictionary.GetFloatOrDefault(PdfTokens.FnNKey);

            if (c0 == null || c0.Length == 0)
            {
                c0 = new float[] { 0f };
            }
            if (c1 == null || c1.Length == 0)
            {
                c1 = new float[] { 1f };
            }

            int componentCount = Math.Min(c0.Length, c1.Length);
            if (componentCount == 0)
            {
                return null;
            }

            return new ExponentialPdfFunction(c0, c1, exponent);
        }
    }
}

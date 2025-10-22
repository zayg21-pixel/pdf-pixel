using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Enumerates supported PDF function types.
    /// </summary>
    public enum PdfFunctionType
    {
        Unknown = -1,
        Sampled = 0,
        Exponential = 2,
        Stitching = 3
        // Add more as needed
    }

    /// <summary>
    /// Abstract base class for all PDF function types. Provides evaluation interface and helpers.
    /// </summary>
    public abstract class PdfFunction
    {
        /// <summary>
        /// Evaluate the function for a single input value.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns>Evaluated output values.</returns>
        public abstract ReadOnlySpan<float> Evaluate(float value);

        /// <summary>
        /// Evaluate the function for multiple input values.
        /// </summary>
        /// <param name="values">Input values.</param>
        /// <returns>Evaluated output values.</returns>
        public abstract ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values);

        /// <summary>
        /// Gets the function type from the specified PDF function object.
        /// </summary>
        /// <param name="functionObject">PDF function object.</param>
        /// <returns>Function type enum value.</returns>
        public static PdfFunctionType GetFunctionType(PdfObject functionObject)
        {
            if (functionObject == null || functionObject.Dictionary == null)
            {
                return PdfFunctionType.Unknown;
            }

            int typeValue = functionObject.Dictionary.GetIntegerOrDefault(PdfTokens.FunctionTypeKey);

            switch (typeValue)
            {
                case 0:
                    return PdfFunctionType.Sampled;
                case 2:
                    return PdfFunctionType.Exponential;
                case 3:
                    return PdfFunctionType.Stitching;
                default:
                    return PdfFunctionType.Unknown;
            }
        }
    }
}

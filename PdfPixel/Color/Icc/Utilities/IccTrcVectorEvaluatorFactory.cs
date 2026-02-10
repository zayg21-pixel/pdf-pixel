using System;
using System.Linq;
using PdfPixel.Color.Icc.Model;

namespace PdfPixel.Color.Icc.Utilities
{
    /// <summary>
    /// Factory for creating IIccTrcVectorEvaluator instances for ICC TRC definitions (multi-channel).
    /// </summary>
    internal static class IccTrcVectorEvaluatorFactory
    {
        /// <summary>
        /// Returns a vector evaluator for the given TRC definitions.
        /// </summary>
        /// <param name="trcs">Array of ICC TRC definitions (0-4 channels).</param>
        /// <returns>Vector evaluator instance for the curves.</returns>
        public static IIccTrcVectorEvaluator Create(params IccTrc[] trcs)
        {
            if (!IsVectorizable(trcs) || trcs.Length == 1)
            {
                return new PerChannelTrcVectorEvaluator(trcs);
            }

            var type = trcs[0].Type;
            switch (type)
            {
                case IccTrcType.Gamma:
                {
                    float[] gammas = new float[trcs.Length];
                    for (int i = 0; i < trcs.Length; i++)
                    {
                        gammas[i] = trcs[i].Gamma;
                    }
                    return new GammaTrcVectorEvaluator(gammas);
                }
                case IccTrcType.Sampled:
                {
                    float[][] samples = new float[trcs.Length][];
                    for (int i = 0; i < trcs.Length; i++)
                    {
                        samples[i] = trcs[i].Samples;
                    }
                    return new SampledTrcVectorEvaluator(samples);
                }
                case IccTrcType.Parametric:
                {
                    var paramType = trcs[0].ParametricType;
                    IccTrcParameters[] parameters = new IccTrcParameters[trcs.Length];
                    for (int i = 0; i < trcs.Length; i++)
                    {
                        parameters[i] = trcs[i].TrcParameters;
                    }

                    switch (paramType)
                    {
                        case IccTrcParametricType.Gamma:
                            return new GammaTrcVectorEvaluator(parameters.Select(x => x.Gamma).ToArray());
                        case IccTrcParametricType.PowerWithOffset:
                            return new PowerWithOffsetTrcVectorEvaluator(parameters);
                        case IccTrcParametricType.PowerWithOffsetAndC:
                            return new PowerWithOffsetAndCTrcVectorEvaluator(parameters);
                        case IccTrcParametricType.PowerWithLinearSegment:
                            return new PowerWithLinearSegmentTrcVectorEvaluator(parameters);
                        case IccTrcParametricType.PowerWithLinearSegmentAndOffset:
                            return new PowerWithLinearSegmentAndOffsetTrcVectorEvaluator(parameters);
                        default:
                            throw new NotSupportedException($"Parametric type {paramType} is not supported.");
                    }
                }
                case IccTrcType.None:
                default:
                    throw new NotSupportedException($"IccTrcType {type} is not supported for vector evaluators.");
            }
        }

        /// <summary>
        /// Checks if the provided TRC definitions can be vectorized together.
        /// </summary>
        /// <param name="trcs">Array of ICC TRC definitions (1-4 channels).</param>
        /// <returns>True if all TRCs have the same type and parametric type (if applicable), and the array is not empty; otherwise, false.</returns>
        private static bool IsVectorizable(params IccTrc[] trcs)
        {
            if (trcs == null || trcs.Length == 0 || trcs.Length > 4)
            {
                return false;
            }
            var type = trcs[0].Type;
            for (int i = 1; i < trcs.Length; i++)
            {
                if (trcs[i].Type != type)
                {
                    return false;
                }
            }
            if (type == IccTrcType.Parametric)
            {
                var paramType = trcs[0].ParametricType;
                for (int i = 1; i < trcs.Length; i++)
                {
                    if (trcs[i].ParametricType != paramType)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}

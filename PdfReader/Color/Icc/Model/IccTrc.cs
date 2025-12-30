using PdfReader.Color.Icc.Utilities;
using PdfReader.Functions;

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Represents a tone reproduction curve (TRC) abstraction for an ICC profile channel (Gray or RGB).
/// Encapsulates gamma, sampled, or parametric curve data for color transformations.
/// </summary>
internal enum IccTrcType
{
    /// <summary>
    /// Unspecified or unknown TRC kind; treated as identity (linear) by evaluators.
    /// </summary>
    None,
    /// <summary>
    /// Simple gamma exponent curve where y = x^Gamma.
    /// </summary>
    Gamma,
    /// <summary>
    /// Sampled curve with equally spaced samples over input domain [0..1].
    /// </summary>
    Sampled,
    /// <summary>
    /// Parametric curve defined by ICC parametricCurveType and associated parameters.
    /// </summary>
    Parametric,
}

/// <summary>
/// ICC parametric curve type identifiers per ICC spec (0..4 currently supported).
/// </summary>
internal enum IccTrcParametricType
{
    /// <summary>
    /// Not a parametric curve.
    /// </summary>
    None = -1,

    /// <summary>
    /// y = x^g
    /// </summary>
    Gamma = 0,
    /// <summary>
    /// y = (a·x + b)^g for x ? ?b/a; else 0
    /// </summary>
    PowerWithOffset = 1,
    /// <summary>
    /// y = (a·x + b)^g + c for x ? ?b/a; else c
    /// </summary>
    PowerWithOffsetAndC = 2,
    /// <summary>
    /// y = (a·x + b)^g for x ? d; else c·x
    /// </summary>
    PowerWithLinearSegment = 3,
    /// <summary>
    /// y = (a·x + b)^g + e for x ? d; else c·x + f
    /// </summary>
    PowerWithLinearSegmentAndOffset = 4
}

/// <summary>
/// Represents a tone reproduction curve (TRC) for an ICC profile channel.
/// Supports gamma, sampled, and parametric curve forms for color processing.
/// </summary>
internal sealed class IccTrc
{
    private const int TargetSampleCount = 1024;

    private IccTrc(
        IccTrcType type,
        float gamma,
        float[] samples,
        IccTrcParametricType paramType,
        float[] parameters)
    {
        Type = type;
        Gamma = gamma;
        Samples = samples;
        ParametricType = paramType;
        Parameters = parameters;

        if (type == IccTrcType.Sampled)
        {
            if (Samples != null && Samples.Length > 0 && Samples.Length < TargetSampleCount)
            {
                Samples = ResampleCubic(Samples, TargetSampleCount);
            }
        }

        if (type == IccTrcType.Parametric)
        {
            TrcParameters = new IccTrcParameters(parameters, paramType);
            Gamma = TrcParameters.Gamma;
        }

        Evaluator = IccTrcEvaluatorFactory.Create(this);
    }

    /// <summary>
    /// TRC kind discriminator.
    /// </summary>
    public IccTrcType Type { get; }

    /// <summary>
    /// Value evaluator for this TRC.
    /// </summary>
    public IIccTrcEvaluator Evaluator { get; }

    /// <summary>
    /// Named parameters for parametric TRC curves. Null for non-parametric types.
    /// </summary>
    public IccTrcParameters TrcParameters { get; }

    /// <summary>
    /// The gamma exponent when <see cref="Type"/> is <see cref="IccTrcType.Gamma"/> is true.
    /// </summary>
    public float Gamma { get; }

    /// <summary>
    /// Sample values (normalized 0..1) for a sampled curve, or null for gamma/parametric/placeholder sampled descriptors.
    /// </summary>
    public float[] Samples { get; }

    /// <summary>
    /// Parametric curve type identifier (matches ICC spec enumeration 0..4 for supported types; value retained for unsupported as well).
    /// </summary>
    public IccTrcParametricType ParametricType { get; }

    /// <summary>
    /// Parameter array for parametric curves (contents depend on <see cref="ParametricType"/>).
    /// </summary>
    public float[] Parameters { get; }

    /// <summary>
    /// Create a gamma TRC representation.
    /// </summary>
    public static IccTrc FromGamma(float gamma)
    {
        return new IccTrc(IccTrcType.Gamma, gamma, null, IccTrcParametricType.None, null);
    }

    /// <summary>
    /// Create a sampled TRC from an explicit sample array.
    /// </summary>
    public static IccTrc FromSamples(float[] samples)
    {
        float[] sampleArray = samples ?? System.Array.Empty<float>();
        return new IccTrc(IccTrcType.Sampled, 0f, sampleArray, IccTrcParametricType.None, null);
    }

    /// <summary>
    /// Create a supported parametric TRC representation.
    /// </summary>
    public static IccTrc FromParametric(IccTrcParametricType type, float[] parameters)
    {
        return new IccTrc(IccTrcType.Parametric, 0f, null, type, parameters ?? System.Array.Empty<float>());
    }

    /// <summary>
    /// Resamples a 1D array of samples to the specified target length using Catmull-Rom bicubic interpolation.
    /// Input samples are assumed to be uniformly spaced over [0..1].
    /// </summary>
    private static float[] ResampleCubic(float[] src, int targetLength)
    {
        int n = src.Length;
        if (n == 0 || targetLength <= 0)
        {
            return System.Array.Empty<float>();
        }
        if (n == 1)
        {
            float[] single = new float[targetLength];
            for (int i = 0; i < targetLength; i++)
            {
                single[i] = src[0];
            }
            return single;
        }

        float[] dst = new float[targetLength];
        float scale = (n - 1) / (float)(targetLength - 1);

        for (int i = 0; i < targetLength; i++)
        {
            float u = i * scale; // position in source index space
            int i1 = (int)u; // base index
            float t = u - i1; // local fraction

            int i0 = i1 - 1;
            int i2 = i1 + 1;
            int i3 = i1 + 2;

            if (i1 >= n - 1)
            {
                // Clamp to last segment
                i1 = n - 2;
                i0 = i1 - 1;
                i2 = i1 + 1;
                i3 = i2; // duplicate last
                t = 1f;
            }

            if (i0 < 0)
            {
                i0 = 0;
            }
            if (i3 >= n)
            {
                i3 = n - 1;
            }

            float p0 = src[i0];
            float p1 = src[i1];
            float p2 = src[i2];
            float p3 = src[i3];

            dst[i] = CatmullRom(p0, p1, p2, p3, t);
        }

        return dst;
    }

    /// <summary>
    /// Catmull-Rom spline interpolation for four successive samples.
    /// </summary>
    private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        // Standard Catmull-Rom with tension = 0.5
        float a0 = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
        float a1 = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
        float a2 = -0.5f * p0 + 0.5f * p2;
        float a3 = p1;
        float value = a0 * t3 + a1 * t2 + a2 * t + a3;
        if (value < 0f)
        {
            return 0f;
        }
        if (value > 1f)
        {
            return 1f;
        }
        return value;
    }
}

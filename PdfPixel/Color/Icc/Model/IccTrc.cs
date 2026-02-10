using PdfPixel.Color.Icc.Utilities;
using PdfPixel.Functions;

namespace PdfPixel.Color.Icc.Model;

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
    public const int MinSampleCount = 1024;

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
            if (Samples != null && Samples.Length > 0 && Samples.Length < MinSampleCount)
            {
                Samples = SamplesUpsampler.ResampleCubic(Samples, MinSampleCount);
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
    /// Guaranteed to have at least <see cref="MinSampleCount"/> entries when not null.
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
}

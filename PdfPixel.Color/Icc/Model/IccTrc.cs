using PdfPixel.Color.Icc.Trc;

namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// Represents a tone reproduction curve (TRC) for an ICC profile channel.
/// Supports gamma, sampled, and parametric curve forms for color processing.
/// </summary>
public sealed class IccTrc
{
    private IccTrc(
        IccTrcType type,
        float gamma,
        float[]? samples,
        IccTrcParametricType paramType,
        float[]? parameters)
    {
        Type = type;
        Gamma = gamma;
        Samples = samples;
        ParametricType = paramType;
        Parameters = parameters;

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
    public IccTrcParameters? TrcParameters { get; }

    /// <summary>
    /// The gamma exponent when <see cref="Type"/> is <see cref="IccTrcType.Gamma"/> is true.
    /// </summary>
    public float Gamma { get; }

    /// <summary>
    /// Sample values (normalized 0..1) for a sampled curve, or null for gamma/parametric/placeholder sampled descriptors.
    /// </summary>
    public float[]? Samples { get; }

    /// <summary>
    /// Parametric curve type identifier (matches ICC spec enumeration 0..4 for supported types; value retained for unsupported as well).
    /// </summary>
    public IccTrcParametricType ParametricType { get; }

    /// <summary>
    /// Parameter array for parametric curves (contents depend on <see cref="ParametricType"/>).
    /// </summary>
    public float[]? Parameters { get; }

    /// <summary>
    /// Create a gamma TRC representation.
    /// </summary>
    public static IccTrc FromGamma(float gamma) => new(IccTrcType.Gamma, gamma, null, IccTrcParametricType.None, null);

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
    public static IccTrc FromParametric(IccTrcParametricType type, float[]? parameters) => new(IccTrcType.Parametric, 0f, null, type, parameters ?? System.Array.Empty<float>());
}

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Represents a tone reproduction curve (TRC) associated with an ICC profile channel (Gray / RGB).
/// A TRC can be one of:
///  - Simple gamma value (IsGamma)
///  - Sampled curve (IsSampled) with equally spaced samples (Samples)
///  - Parametric curve (IsParametric) with parameters per ICC parametricCurveType (0..4 supported here)
///  - Unsupported parametric variant recorded only for diagnostics (IsUnsupportedParametric)
/// Helper factory methods construct appropriately flagged instances; evaluation is performed elsewhere.
/// </summary>
internal sealed class IccTrc
{
    private IccTrc(
        bool isGamma,
        float gamma,
        bool isSampled,
        int sampleCount,
        float[] samples,
        bool isParametric,
        int paramType,
        float[] parameters,
        bool isUnsupportedParametric)
    {
        IsGamma = isGamma;
        Gamma = gamma;
        IsSampled = isSampled;
        SampleCount = sampleCount;
        Samples = samples;
        IsParametric = isParametric;
        ParametricType = paramType;
        Parameters = parameters;
        IsUnsupportedParametric = isUnsupportedParametric;
    }

    /// <summary>
    /// True when the TRC is a simple gamma value.
    /// </summary>
    public bool IsGamma { get; }

    /// <summary>
    /// The gamma exponent when <see cref="IsGamma"/> is true.
    /// </summary>
    public float Gamma { get; }

    /// <summary>
    /// True when the TRC is a sampled curve (equally spaced samples covering 0..1 input domain).
    /// </summary>
    public bool IsSampled { get; }

    /// <summary>
    /// Number of samples reported for a sampled curve (may be non-zero even if <see cref="Samples"/> is null when placeholder only).
    /// </summary>
    public int SampleCount { get; }

    /// <summary>
    /// Sample values (normalized 0..1) for a sampled curve, or null for gamma/parametric/placeholder sampled descriptors.
    /// </summary>
    public float[] Samples { get; }

    /// <summary>
    /// True when the TRC is a supported parametric curve (parametricCurveType 0..4).
    /// </summary>
    public bool IsParametric { get; }

    /// <summary>
    /// Parametric curve type identifier (matches ICC spec enumeration 0..4 for supported types; value retained for unsupported as well).
    /// </summary>
    public int ParametricType { get; }

    /// <summary>
    /// Parameter array for parametric curves (contents depend on <see cref="ParametricType"/>).
    /// </summary>
    public float[] Parameters { get; }

    /// <summary>
    /// True when the TRC refers to a parametric type not supported for evaluation (recorded for completeness).
    /// </summary>
    public bool IsUnsupportedParametric { get; }

    /// <summary>
    /// Create a gamma TRC representation.
    /// </summary>
    public static IccTrc FromGamma(float gamma)
    {
        return new IccTrc(true, gamma, false, 0, null, false, 0, null, false);
    }

    /// <summary>
    /// Create a sampled TRC from an explicit sample array.
    /// </summary>
    public static IccTrc FromSamples(float[] samples)
    {
        float[] sampleArray = samples ?? System.Array.Empty<float>();
        return new IccTrc(false, 0f, true, sampleArray.Length, sampleArray, false, 0, null, false);
    }

    /// <summary>
    /// Create a supported parametric TRC representation.
    /// </summary>
    public static IccTrc FromParametric(int type, float[] parameters)
    {
        return new IccTrc(false, 0f, false, 0, null, true, type, parameters ?? System.Array.Empty<float>(), false);
    }

    /// <summary>
    /// Create a placeholder sampled TRC with a specified sample count but without actual sample values.
    /// Used when the profile provides a sampled curve not yet expanded.
    /// </summary>
    public static IccTrc Sampled(int count)
    {
        int safeCount = count < 0 ? 0 : count;
        return new IccTrc(false, 0f, true, safeCount, null, false, 0, null, false);
    }

    /// <summary>
    /// Create a placeholder for an unsupported parametric TRC type.
    /// </summary>
    public static IccTrc UnsupportedParametric(int type)
    {
        return new IccTrc(false, 0f, false, 0, null, false, type, null, true);
    }
}

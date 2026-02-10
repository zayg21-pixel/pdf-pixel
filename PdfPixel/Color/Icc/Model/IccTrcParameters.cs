namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// Encapsulates named parameters for ICC parametric tone reproduction curves (TRC).
/// Provides descriptive properties for all supported ICC parametric curve types (0..4).
/// </summary>
internal class IccTrcParameters
{
    /// <summary>
    /// Exponent (gamma) parameter. Used in all parametric types.
    /// </summary>
    public float Gamma { get; }

    /// <summary>
    /// Scale factor (a). Used in types 1, 2, 3, 4.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Offset (b). Used in types 1, 2, 3, 4.
    /// </summary>
    public float Offset { get; }

    /// <summary>
    /// Constant (c). Used in types 2, 3, 4.
    /// </summary>
    public float ConstantC { get; }

    /// <summary>
    /// Breakpoint (d). Used in types 3, 4.
    /// </summary>
    public float Breakpoint { get; }

    /// <summary>
    /// Offset (e) added to the power segment in type 4.
    /// </summary>
    public float PowerOffset { get; }

    /// <summary>
    /// Linear offset (f). Used in type 4.
    /// </summary>
    public float LinearOffset { get; }

    /// <summary>
    /// Creates a new instance with default values.
    /// </summary>
    public IccTrcParameters()
        : this(1f, 1f, 0f, 0f, 0f, 0f, 0f) { }

    /// <summary>
    /// Creates a new instance with all parameters specified.
    /// </summary>
    public IccTrcParameters(
        float gamma,
        float scale,
        float offset,
        float constantC,
        float breakpoint,
        float powerOffset,
        float linearOffset)
    {
        Gamma = gamma;
        Scale = scale;
        Offset = offset;
        ConstantC = constantC;
        Breakpoint = breakpoint;
        PowerOffset = powerOffset;
        LinearOffset = linearOffset;
    }

    /// <summary>
    /// Creates a new instance by decomposing the ICC parametric curve parameters array.
    /// </summary>
    /// <param name="parameters">Parameter array as defined by ICC spec for the parametric type.</param>
    /// <param name="type">ICC parametric curve type.</param>
    public IccTrcParameters(float[] parameters, IccTrcParametricType type)
    {
        if (parameters == null || parameters.Length == 0)
        {
            Gamma = 1f;
            Scale = 1f;
            Offset = 0f;
            ConstantC = 0f;
            Breakpoint = 0f;
            PowerOffset = 0f;
            LinearOffset = 0f;
            return;
        }

        switch (type)
        {
            case IccTrcParametricType.Gamma:
                Gamma = parameters.Length > 0 ? parameters[0] : 1f;
                Scale = 0f;
                Offset = 0f;
                ConstantC = 0f;
                Breakpoint = 0f;
                PowerOffset = 0f;
                LinearOffset = 0f;
                break;
            case IccTrcParametricType.PowerWithOffset:
                Gamma = parameters.Length > 0 ? parameters[0] : 1f;
                Scale = parameters.Length > 1 ? parameters[1] : 0f;
                Offset = parameters.Length > 2 ? parameters[2] : 0f;
                ConstantC = 0f;
                Breakpoint = (Scale != 0f) ? -Offset / Scale : 0f;
                PowerOffset = 0f;
                LinearOffset = 0f;
                break;
            case IccTrcParametricType.PowerWithOffsetAndC:
                Gamma = parameters.Length > 0 ? parameters[0] : 1f;
                Scale = parameters.Length > 1 ? parameters[1] : 0f;
                Offset = parameters.Length > 2 ? parameters[2] : 0f;
                ConstantC = parameters.Length > 3 ? parameters[3] : 0f;
                Breakpoint = (Scale != 0f) ? -Offset / Scale : 0f;
                PowerOffset = 0f;
                LinearOffset = 0f;
                break;
            case IccTrcParametricType.PowerWithLinearSegment:
                Gamma = parameters.Length > 0 ? parameters[0] : 1f;
                Scale = parameters.Length > 1 ? parameters[1] : 0f;
                Offset = parameters.Length > 2 ? parameters[2] : 0f;
                ConstantC = parameters.Length > 3 ? parameters[3] : 0f;
                Breakpoint = parameters.Length > 4 ? parameters[4] : 0f;
                PowerOffset = 0f;
                LinearOffset = 0f;
                break;
            case IccTrcParametricType.PowerWithLinearSegmentAndOffset:
                Gamma = parameters.Length > 0 ? parameters[0] : 1f;
                Scale = parameters.Length > 1 ? parameters[1] : 0f;
                Offset = parameters.Length > 2 ? parameters[2] : 0f;
                ConstantC = parameters.Length > 3 ? parameters[3] : 0f;
                Breakpoint = parameters.Length > 4 ? parameters[4] : 0f;
                PowerOffset = parameters.Length > 5 ? parameters[5] : 0f;
                LinearOffset = parameters.Length > 6 ? parameters[6] : 0f;
                break;
            default:
                Gamma = 1f;
                Scale = 0f;
                Offset = 0f;
                ConstantC = 0f;
                Breakpoint = 0f;
                PowerOffset = 0f;
                LinearOffset = 0f;
                break;
        }
    }
}

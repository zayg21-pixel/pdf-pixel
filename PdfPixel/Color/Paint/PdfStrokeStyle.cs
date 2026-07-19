namespace PdfPixel.Color.Paint;

/// <summary>
/// Stroke-specific styling for a <see cref="PdfPaint"/> used as a stroking paint.
/// Mutable; content-stream operators (w, J, j, M, d) update fields in place.
/// </summary>
public sealed class PdfStrokeStyle
{
    /// <summary>
    /// Line width (w operator). Default 1.
    /// </summary>
    public float LineWidth { get; set; } = 1.0f;

    /// <summary>
    /// Line cap style (J operator). Default Butt.
    /// </summary>
    public PdfStrokeCap LineCap { get; set; } = PdfStrokeCap.Butt;

    /// <summary>
    /// Line join style (j operator). Default Miter.
    /// </summary>
    public PdfStrokeJoin LineJoin { get; set; } = PdfStrokeJoin.Miter;

    /// <summary>
    /// Miter limit (M operator). Default 10.
    /// </summary>
    public float MiterLimit { get; set; } = 10.0f;

    /// <summary>
    /// Dash pattern array (d operator). Null means solid line.
    /// </summary>
    public float[]? DashPattern { get; set; }

    /// <summary>
    /// Dash phase (d operator). Default 0.
    /// </summary>
    public float DashPhase { get; set; }

    /// <summary>
    /// Border style (annotation BS dictionary Style entry). Default Solid; meaningless outside
    /// annotation border rendering.
    /// </summary>
    public PdfBorderStyleType BorderStyleType { get; set; } = PdfBorderStyleType.Solid;

    /// <summary>
    /// Border effect (annotation BE dictionary Style entry). Default Solid (no effect); meaningless
    /// outside annotation border rendering.
    /// </summary>
    public PdfBorderEffectType BorderEffectType { get; set; } = PdfBorderEffectType.Solid;

    /// <summary>
    /// Border effect intensity in the range 0–2 (annotation BE dictionary I entry). Only meaningful
    /// when <see cref="BorderEffectType"/> is <see cref="PdfBorderEffectType.Cloudy"/>.
    /// </summary>
    public float BorderEffectIntensity { get; set; }

    /// <summary>
    /// Creates an independent copy of this stroke style.
    /// </summary>
    public PdfStrokeStyle Clone()
    {
        return new()
        {
            LineWidth = LineWidth,
            LineCap = LineCap,
            LineJoin = LineJoin,
            MiterLimit = MiterLimit,
            DashPattern = (DashPattern != null) ? (float[])DashPattern.Clone() : null,
            DashPhase = DashPhase,
            BorderStyleType = BorderStyleType,
            BorderEffectType = BorderEffectType,
            BorderEffectIntensity = BorderEffectIntensity
        };
    }
}

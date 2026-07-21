using PdfPixel.Rendering.State;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Stroke-specific styling for a <see cref="PdfPaint"/> used as a stroking paint.
/// Immutable; the <c>With*</c> methods return a new instance.
/// </summary>
public sealed class PdfStrokeStyle
{
    /// <summary>
    /// Initializes a new stroke style.
    /// </summary>
    public PdfStrokeStyle(
        float lineWidth = 1.0f,
        PdfStrokeCap lineCap = PdfStrokeCap.Butt,
        PdfStrokeJoin lineJoin = PdfStrokeJoin.Miter,
        float miterLimit = 10.0f,
        float[]? dashPattern = null,
        float dashPhase = 0f,
        PdfStrokeEffectType effectType = PdfStrokeEffectType.Solid,
        float effectIntensity = 0f)
    {
        LineWidth = lineWidth;
        LineCap = lineCap;
        LineJoin = lineJoin;
        MiterLimit = miterLimit;
        DashPattern = dashPattern;
        DashPhase = dashPhase;
        EffectType = effectType;
        EffectIntensity = effectIntensity;
    }

    /// <summary>
    /// Line width (w operator). Default 1.
    /// </summary>
    public float LineWidth { get; }

    /// <summary>
    /// Line cap style (J operator). Default Butt.
    /// </summary>
    public PdfStrokeCap LineCap { get; }

    /// <summary>
    /// Line join style (j operator). Default Miter.
    /// </summary>
    public PdfStrokeJoin LineJoin { get; }

    /// <summary>
    /// Miter limit (M operator). Default 10.
    /// </summary>
    public float MiterLimit { get; }

    /// <summary>
    /// Dash pattern array (d operator). Null means solid line.
    /// </summary>
    public float[]? DashPattern { get; }

    /// <summary>
    /// Dash phase (d operator). Default 0.
    /// </summary>
    public float DashPhase { get; }

    /// <summary>
    /// Effect applied to this stroke. Default Solid (no effect).
    /// </summary>
    public PdfStrokeEffectType EffectType { get; }

    /// <summary>
    /// Effect intensity in the range 0–2. Only meaningful when <see cref="EffectType"/>
    /// is <see cref="PdfStrokeEffectType.Cloudy"/>.
    /// </summary>
    public float EffectIntensity { get; }

    /// <summary>
    /// Returns a copy of this style with <see cref="LineWidth"/> replaced.
    /// </summary>
    public PdfStrokeStyle WithLineWidth(float lineWidth)
        => new(lineWidth, LineCap, LineJoin, MiterLimit, DashPattern, DashPhase, EffectType, EffectIntensity);

    /// <summary>
    /// Returns a copy of this style with <see cref="LineCap"/> replaced.
    /// </summary>
    public PdfStrokeStyle WithLineCap(PdfStrokeCap lineCap)
        => new(LineWidth, lineCap, LineJoin, MiterLimit, DashPattern, DashPhase, EffectType, EffectIntensity);

    /// <summary>
    /// Returns a copy of this style with <see cref="LineJoin"/> replaced.
    /// </summary>
    public PdfStrokeStyle WithLineJoin(PdfStrokeJoin lineJoin)
        => new(LineWidth, LineCap, lineJoin, MiterLimit, DashPattern, DashPhase, EffectType, EffectIntensity);

    /// <summary>
    /// Returns a copy of this style with <see cref="MiterLimit"/> replaced.
    /// </summary>
    public PdfStrokeStyle WithMiterLimit(float miterLimit)
        => new(LineWidth, LineCap, LineJoin, miterLimit, DashPattern, DashPhase, EffectType, EffectIntensity);

    /// <summary>
    /// Returns a copy of this style with <see cref="DashPattern"/> and <see cref="DashPhase"/> replaced together.
    /// </summary>
    public PdfStrokeStyle WithDash(float[]? dashPattern, float dashPhase)
        => new(LineWidth, LineCap, LineJoin, MiterLimit, dashPattern, dashPhase, EffectType, EffectIntensity);

    /// <summary>
    /// Returns a copy of this style with <see cref="EffectType"/> and <see cref="EffectIntensity"/> replaced together.
    /// </summary>
    public PdfStrokeStyle WithEffect(PdfStrokeEffectType effectType, float effectIntensity)
        => new(LineWidth, LineCap, LineJoin, MiterLimit, DashPattern, DashPhase, effectType, effectIntensity);

    /// <summary>
    /// Returns a copy of this style with every stroke-related field present in <paramref name="parameters"/>
    /// (from a <c>gs</c> ExtGState application) applied. Fields absent from <paramref name="parameters"/> are
    /// left unchanged. Returns this instance unchanged when no stroke-related field is present.
    /// </summary>
    internal PdfStrokeStyle WithGraphicsState(PdfGraphicsStateParameters parameters)
    {
        if (!parameters.LineWidth.HasValue
            && !parameters.LineCap.HasValue
            && !parameters.LineJoin.HasValue
            && !parameters.MiterLimit.HasValue
            && parameters.DashPattern == null)
        {
            return this;
        }

        float[]? dashPattern = parameters.DashPattern ?? DashPattern;
        float dashPhase = (parameters.DashPattern != null) ? parameters.DashPhase.GetValueOrDefault() : DashPhase;

        return new PdfStrokeStyle(
            parameters.LineWidth ?? LineWidth,
            parameters.LineCap ?? LineCap,
            parameters.LineJoin ?? LineJoin,
            parameters.MiterLimit ?? MiterLimit,
            dashPattern,
            dashPhase,
            EffectType,
            EffectIntensity);
    }
}

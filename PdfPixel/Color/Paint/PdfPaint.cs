using System;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Represents the current paint (color or pattern) for stroke/fill operations, plus the paint-level
/// state that feeds SKPaint construction for that operation (alpha, blend mode, overprint, stroke styling).
/// Immutable; the <c>With*</c> methods return a new instance.
/// </summary>
public sealed class PdfPaint
{
    private PdfPaint(
        PdfPaintStyle style,
        in PdfColor color,
        float alpha,
        PdfBlendMode blendMode,
        bool overprint,
        PdfPattern? pattern,
        PdfStrokeStyle? strokeStyle,
        PdfPaintShadowEffect? shadowEffect)
    {
        Style = style;
        Color = color;
        Alpha = alpha;
        BlendMode = blendMode;
        Overprint = overprint;
        Pattern = pattern;
        StrokeStyle = strokeStyle;
        ShadowEffect = shadowEffect;
    }

    /// <summary>
    /// Initializes a new paint for the given fill/stroke role. <see cref="StrokeStyle"/> is populated only
    /// when <paramref name="style"/> is <see cref="PdfPaintStyle.Stroke"/>.
    /// </summary>
    public PdfPaint(PdfPaintStyle style)
        : this(style, default, 1.0f, PdfBlendMode.Normal, false, null, (style == PdfPaintStyle.Stroke) ? new PdfStrokeStyle() : null, null)
    {
    }

    /// <summary>
    /// Initializes a new stroke paint that uses <paramref name="strokeStyle"/> directly (no copy).
    /// </summary>
    public PdfPaint(PdfStrokeStyle strokeStyle)
        : this(PdfPaintStyle.Stroke, default, 1.0f, PdfBlendMode.Normal, false, null, strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle)), null)
    {
    }

    /// <summary>
    /// Gets whether this paint is used for filling or stroking. Fixed for the lifetime of the instance.
    /// </summary>
    public PdfPaintStyle Style { get; }

    /// <summary>
    /// Gets the resolved sRGB color for solid color paints, or a placeholder color for pattern paints.
    /// For uncolored (stencil) patterns this color typically represents the resolved tint in the base color space.
    /// </summary>
    public PdfColor Color { get; }

    /// <summary>
    /// Gets the constant alpha (ca/CA) applied on top of <see cref="Color"/>'s own alpha channel.
    /// </summary>
    public float Alpha { get; }

    /// <summary>
    /// Gets the blend mode (BM) used when compositing this paint.
    /// </summary>
    public PdfBlendMode BlendMode { get; }

    /// <summary>
    /// Gets the overprint flag (OP for stroke, op for fill) for this paint.
    /// </summary>
    public bool Overprint { get; }

    /// <summary>
    /// Gets the pattern definition when the paint represents a pattern; null for solid color paints.
    /// </summary>
    public PdfPattern? Pattern { get; }

    /// <summary>
    /// Gets the stroke-specific styling (line width, cap, join, miter limit, dash). Non-null when
    /// <see cref="Style"/> is <see cref="PdfPaintStyle.Stroke"/>; null for fill paints.
    /// </summary>
    public PdfStrokeStyle? StrokeStyle { get; }

    /// <summary>
    /// Gets an optional drop-shadow effect applied to this paint. Null means no shadow.
    /// </summary>
    public PdfPaintShadowEffect? ShadowEffect { get; }

    /// <summary>
    /// True if this paint represents a pattern (colored or uncolored), otherwise false for solid colors.
    /// </summary>
    public bool IsPattern => Pattern != null;

    /// <summary>
    /// Returns <see cref="StrokeStyle"/>, guaranteed non-null because <see cref="Style"/> is
    /// <see cref="PdfPaintStyle.Stroke"/>. Throws if called on a fill paint.
    /// </summary>
    public PdfStrokeStyle RequireStrokeStyle()
    {
        if (StrokeStyle == null)
        {
            throw new InvalidOperationException($"{nameof(StrokeStyle)} is only available on paints with {nameof(PdfPaintStyle)}.{nameof(PdfPaintStyle.Stroke)}.");
        }

        return StrokeStyle;
    }

    /// <summary>
    /// Returns a copy of this paint set to a solid color, clearing any pattern. Alpha, blend mode, overprint,
    /// and stroke styling are left untouched.
    /// </summary>
    public PdfPaint WithSolidColor(in PdfColor color)
        => new(Style, color, Alpha, BlendMode, Overprint, null, StrokeStyle, ShadowEffect);

    /// <summary>
    /// Returns a copy of this paint set to a pattern (colored or uncolored). For uncolored patterns provide the
    /// resolved tint color. Alpha, blend mode, overprint, and stroke styling are left untouched.
    /// </summary>
    public PdfPaint WithPattern(PdfPattern pattern, in PdfColor resolvedTintColor)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return new PdfPaint(Style, resolvedTintColor, Alpha, BlendMode, Overprint, pattern, StrokeStyle, ShadowEffect);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="ShadowEffect"/> replaced.
    /// </summary>
    public PdfPaint WithShadowEffect(PdfPaintShadowEffect? shadowEffect)
        => new(Style, Color, Alpha, BlendMode, Overprint, Pattern, StrokeStyle, shadowEffect);

    /// <summary>
    /// Returns a copy of this paint with <see cref="Alpha"/> replaced.
    /// </summary>
    public PdfPaint WithAlpha(float alpha)
        => new(Style, Color, alpha, BlendMode, Overprint, Pattern, StrokeStyle, ShadowEffect);

    /// <summary>
    /// Returns a copy of this paint with <see cref="BlendMode"/> replaced.
    /// </summary>
    public PdfPaint WithBlendMode(PdfBlendMode blendMode)
        => new(Style, Color, Alpha, blendMode, Overprint, Pattern, StrokeStyle, ShadowEffect);

    /// <summary>
    /// Returns a copy of this paint with <see cref="StrokeStyle"/> replaced. Only valid on stroke paints.
    /// </summary>
    public PdfPaint WithStrokeStyle(PdfStrokeStyle strokeStyle)
    {
        if (Style != PdfPaintStyle.Stroke)
        {
            throw new InvalidOperationException($"{nameof(WithStrokeStyle)} is only available on paints with {nameof(PdfPaintStyle)}.{nameof(PdfPaintStyle.Stroke)}.");
        }

        return new PdfPaint(Style, Color, Alpha, BlendMode, Overprint, Pattern, strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle)), ShadowEffect);
    }

    /// <summary>
    /// Returns a copy of this paint with every field present in <paramref name="parameters"/> (from a <c>gs</c>
    /// ExtGState application) applied: alpha (ca for fill paints, CA for stroke paints), blend mode, overprint
    /// (op/OP), and stroke styling. Fields absent from <paramref name="parameters"/> are left unchanged.
    /// Returns this instance unchanged when nothing in <paramref name="parameters"/> applies.
    /// </summary>
    internal PdfPaint WithGraphicsState(PdfGraphicsStateParameters parameters)
    {
        bool isStroke = Style == PdfPaintStyle.Stroke;
        float? alpha = isStroke ? parameters.StrokeAlpha : parameters.FillAlpha;
        bool? overprint = isStroke ? parameters.OverprintStroke : parameters.OverprintFill;
        PdfStrokeStyle? strokeStyle = StrokeStyle?.WithGraphicsState(parameters);

        if (alpha == null && overprint == null && parameters.BlendMode == null && ReferenceEquals(strokeStyle, StrokeStyle))
        {
            return this;
        }

        return new PdfPaint(
            Style,
            Color,
            alpha ?? Alpha,
            parameters.BlendMode ?? BlendMode,
            overprint ?? Overprint,
            Pattern,
            strokeStyle,
            ShadowEffect);
    }

    /// <summary>
    /// Create a solid color paint.
    /// </summary>
    public static PdfPaint Solid(in PdfColor color, PdfPaintStyle style) => new PdfPaint(style).WithSolidColor(color);

    /// <summary>
    /// Create a pattern paint (colored or uncolored). For uncolored patterns provide tint components.
    /// </summary>
    public static PdfPaint PatternFill(PdfPattern pattern, in PdfColor resolvedTintColor, PdfPaintStyle style)
        => new PdfPaint(style).WithPattern(pattern, resolvedTintColor);
}

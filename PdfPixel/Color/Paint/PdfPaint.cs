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
        PdfBlendMode blendMode,
        bool overprint,
        PdfPattern? pattern,
        PdfStrokeStyle? strokeStyle,
        PdfPaintShadowEffect? shadowEffect,
        PdfMaskPaintParameters? maskParameters)
    {
        Style = style;
        Color = color;
        BlendMode = blendMode;
        Overprint = overprint;
        Pattern = pattern;
        StrokeStyle = strokeStyle;
        ShadowEffect = shadowEffect;
        MaskParameters = maskParameters;
    }

    /// <summary>
    /// Initializes a new paint for the given fill/stroke role. <see cref="StrokeStyle"/> is populated only
    /// when <paramref name="style"/> is <see cref="PdfPaintStyle.Stroke"/>.
    /// </summary>
    public PdfPaint(PdfPaintStyle style)
        : this(style, PdfColors.Black, PdfBlendMode.Normal, false, null, (style == PdfPaintStyle.Stroke) ? new PdfStrokeStyle() : null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new stroke paint that uses <paramref name="strokeStyle"/> directly.
    /// </summary>
    public PdfPaint(PdfStrokeStyle strokeStyle)
        : this(PdfPaintStyle.Stroke, PdfColors.Black, PdfBlendMode.Normal, false, null, strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle)), null, null)
    {
    }

    /// <summary>
    /// Gets whether this paint is used for filling or stroking.
    /// </summary>
    public PdfPaintStyle Style { get; }

    /// <summary>
    /// Gets the resolved sRGB color for solid color paints, or a placeholder color for pattern paints.
    /// For uncolored (stencil) patterns this color typically represents the resolved tint in the base color space.
    /// </summary>
    public PdfColor Color { get; }

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
    /// Gets the soft-mask compositing parameters (subtype and transfer function) when this paint
    /// composites a previously-rendered soft-mask layer onto content; null for every other paint.
    /// </summary>
    public PdfMaskPaintParameters? MaskParameters { get; }

    /// <summary>
    /// True if this paint represents a pattern (colored or uncolored), otherwise false for solid colors.
    /// </summary>
    public bool IsPattern => Pattern != null;

    /// <summary>
    /// True if this paint carries partial alpha or a blend mode other than <see cref="PdfBlendMode.Normal"/>,
    /// otherwise false.
    /// </summary>
    internal bool RequiresComposition => Color.Alpha < 1 || BlendMode != PdfBlendMode.Normal;

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
    /// Returns a copy of this paint with <see cref="Color"/> replaced.
    /// </summary>
    public PdfPaint WithColor(in PdfColor color)
        => new(Style, color, BlendMode, Overprint, Pattern, StrokeStyle, ShadowEffect, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="Color"/>'s red, green, and blue channels replaced,
    /// clearing any pattern.
    /// </summary>
    public PdfPaint WithSolidColor(in PdfColor color)
        => new(Style, color.WithAlpha(Color.Alpha), BlendMode, Overprint, null, StrokeStyle, ShadowEffect, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="Pattern"/> replaced and <see cref="Color"/>'s red, green,
    /// and blue channels set to <paramref name="resolvedTintColor"/>, the tint of an uncolored pattern.
    /// </summary>
    public PdfPaint WithPattern(PdfPattern pattern, in PdfColor resolvedTintColor)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return new PdfPaint(Style, resolvedTintColor.WithAlpha(Color.Alpha), BlendMode, Overprint, pattern, StrokeStyle, ShadowEffect, MaskParameters);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="ShadowEffect"/> replaced.
    /// </summary>
    public PdfPaint WithShadowEffect(PdfPaintShadowEffect? shadowEffect)
        => new(Style, Color, BlendMode, Overprint, Pattern, StrokeStyle, shadowEffect, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="Color"/>'s alpha channel replaced.
    /// </summary>
    public PdfPaint WithAlpha(float alpha)
        => new(Style, Color.WithAlpha(alpha), BlendMode, Overprint, Pattern, StrokeStyle, ShadowEffect, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="BlendMode"/> replaced.
    /// </summary>
    public PdfPaint WithBlendMode(PdfBlendMode blendMode)
        => new(Style, Color, blendMode, Overprint, Pattern, StrokeStyle, ShadowEffect, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="StrokeStyle"/> replaced. Only valid on stroke paints.
    /// </summary>
    public PdfPaint WithStrokeStyle(PdfStrokeStyle strokeStyle)
    {
        if (Style != PdfPaintStyle.Stroke)
        {
            throw new InvalidOperationException($"{nameof(WithStrokeStyle)} is only available on paints with {nameof(PdfPaintStyle)}.{nameof(PdfPaintStyle.Stroke)}.");
        }

        return new PdfPaint(Style, Color, BlendMode, Overprint, Pattern, strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle)), ShadowEffect, MaskParameters);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="MaskParameters"/> replaced.
    /// </summary>
    public PdfPaint WithMaskParameters(PdfMaskPaintParameters? maskParameters)
        => new(Style, Color, BlendMode, Overprint, Pattern, StrokeStyle, ShadowEffect, maskParameters);

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
            (alpha == null) ? Color : Color.WithAlpha(alpha.Value),
            parameters.BlendMode ?? BlendMode,
            overprint ?? Overprint,
            Pattern,
            strokeStyle,
            ShadowEffect,
            MaskParameters);
    }

    /// <summary>
    /// Create a solid color paint.
    /// </summary>
    public static PdfPaint Solid(in PdfColor color, PdfPaintStyle style) => new PdfPaint(style).WithColor(color);
}

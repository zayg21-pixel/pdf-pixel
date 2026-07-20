using System;
using PdfPixel.Pattern.Model;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Represents the current paint (color or pattern) for stroke/fill operations, plus the paint-level
/// state that feeds SKPaint construction for that operation (alpha, blend mode, overprint, stroke styling).
/// Mutable; content-stream operators update fields in place. Cloned on q (graphics state push) so pushed
/// scopes never alias a parent scope's paint.
/// </summary>
public sealed class PdfPaint
{
    /// <summary>
    /// Initializes a new paint for the given fill/stroke role. <see cref="StrokeStyle"/> is populated only
    /// when <paramref name="style"/> is <see cref="PdfPaintStyle.Stroke"/>.
    /// </summary>
    public PdfPaint(PdfPaintStyle style)
    {
        Style = style;
        StrokeStyle = (style == PdfPaintStyle.Stroke) ? new PdfStrokeStyle() : null;
    }

    /// <summary>
    /// Initializes a new stroke paint that uses <paramref name="strokeStyle"/> directly (no copy).
    /// The caller owns <paramref name="strokeStyle"/> and must not mutate it afterward, since it may be
    /// shared across multiple paints created from the same style.
    /// </summary>
    public PdfPaint(PdfStrokeStyle strokeStyle)
    {
        Style = PdfPaintStyle.Stroke;
        StrokeStyle = strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle));
    }

    /// <summary>
    /// Gets whether this paint is used for filling or stroking. Fixed for the lifetime of the instance.
    /// </summary>
    public PdfPaintStyle Style { get; }

    /// <summary>
    /// Gets or sets the resolved sRGB color for solid color paints, or a placeholder color for pattern paints.
    /// For uncolored (stencil) patterns this color typically represents the resolved tint in the base color space.
    /// </summary>
    public PdfColor Color { get; set; }

    /// <summary>
    /// Gets or sets the constant alpha (ca/CA) applied on top of <see cref="Color"/>'s own alpha channel.
    /// </summary>
    public float Alpha { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the blend mode (BM) used when compositing this paint.
    /// </summary>
    public PdfBlendMode BlendMode { get; set; } = PdfBlendMode.Normal;

    /// <summary>
    /// Gets or sets the overprint flag (OP for stroke, op for fill) for this paint.
    /// </summary>
    public bool Overprint { get; set; }

    /// <summary>
    /// Gets or sets the pattern definition when the paint represents a pattern; null for solid color paints.
    /// </summary>
    public PdfPattern? Pattern { get; set; }

    /// <summary>
    /// Gets the stroke-specific styling (line width, cap, join, miter limit, dash). Non-null when
    /// <see cref="Style"/> is <see cref="PdfPaintStyle.Stroke"/>; null for fill paints.
    /// </summary>
    public PdfStrokeStyle? StrokeStyle { get; private set; }

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
    /// Sets this paint to a solid color in place, clearing any pattern. Leaves alpha, blend mode, overprint,
    /// and stroke styling untouched.
    /// </summary>
    public void SetSolid(in PdfColor color)
    {
        Color = color;
        Pattern = null;
    }

    /// <summary>
    /// Sets this paint to a pattern (colored or uncolored) in place. For uncolored patterns provide the
    /// resolved tint color. Leaves alpha, blend mode, overprint, and stroke styling untouched.
    /// </summary>
    public void SetPattern(PdfPattern pattern, in PdfColor resolvedTintColor)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        Color = resolvedTintColor;
        Pattern = pattern;
    }

    /// <summary>
    /// Create a solid color paint.
    /// </summary>
    public static PdfPaint Solid(in PdfColor color, PdfPaintStyle style) => new(style) { Color = color };

    /// <summary>
    /// Create a pattern paint (colored or uncolored). For uncolored patterns provide tint components.
    /// </summary>
    public static PdfPaint PatternFill(PdfPattern pattern, in PdfColor resolvedTintColor, PdfPaintStyle style)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return new PdfPaint(style) { Color = resolvedTintColor, Pattern = pattern };
    }

    /// <summary>
    /// Creates an independent copy of this paint, so mutating the copy never affects this instance.
    /// </summary>
    public PdfPaint Clone()
    {
        PdfPaint clone = new(Style)
        {
            Color = Color,
            Alpha = Alpha,
            BlendMode = BlendMode,
            Overprint = Overprint,
            Pattern = Pattern
        };

        if (StrokeStyle != null && clone.StrokeStyle != null)
        {
            clone.StrokeStyle.LineWidth = StrokeStyle.LineWidth;
            clone.StrokeStyle.LineCap = StrokeStyle.LineCap;
            clone.StrokeStyle.LineJoin = StrokeStyle.LineJoin;
            clone.StrokeStyle.MiterLimit = StrokeStyle.MiterLimit;
            clone.StrokeStyle.DashPattern = (StrokeStyle.DashPattern != null) ? (float[])StrokeStyle.DashPattern.Clone() : null;
            clone.StrokeStyle.DashPhase = StrokeStyle.DashPhase;
            clone.StrokeStyle.BorderStyleType = StrokeStyle.BorderStyleType;
            clone.StrokeStyle.BorderEffectType = StrokeStyle.BorderEffectType;
            clone.StrokeStyle.BorderEffectIntensity = StrokeStyle.BorderEffectIntensity;
        }

        return clone;
    }
}

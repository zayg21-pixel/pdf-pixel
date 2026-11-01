using System;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Pattern;
using PdfPixel.Rendering;
using PdfPixel.Transparency;

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
        PdfColorSpaceConverter colorSpace,
        PdfDeviceColorMapping deviceColorMapping,
        in PdfColor color,
        float alpha,
        PdfRenderingIntent renderingIntent,
        bool useBlackPointCompensation,
        TransferFunctionTransform? transferFunction,
        PdfBlendMode blendMode,
        bool overprint,
        PdfPattern? pattern,
        PdfStrokeStyle? strokeStyle,
        PdfMaskPaintParameters? maskParameters)
    {
        Style = style;
        ColorSpace = colorSpace;
        DeviceColorMapping = deviceColorMapping;
        Color = color;
        Alpha = alpha;
        RenderingIntent = renderingIntent;
        UseBlackPointCompensation = useBlackPointCompensation;
        TransferFunction = transferFunction;
        BlendMode = blendMode;
        Overprint = overprint;
        Pattern = pattern;
        StrokeStyle = strokeStyle;
        MaskParameters = maskParameters;
    }

    /// <summary>
    /// Initializes a new paint for the given fill/stroke role. <see cref="StrokeStyle"/> is populated only
    /// when <paramref name="style"/> is <see cref="PdfPaintStyle.Stroke"/>.
    /// </summary>
    public PdfPaint(PdfPaintStyle style)
        : this(style, (style == PdfPaintStyle.Stroke) ? new PdfStrokeStyle() : null)
    {
    }

    /// <summary>
    /// Initializes a new stroke paint that uses <paramref name="strokeStyle"/> directly.
    /// </summary>
    public PdfPaint(PdfStrokeStyle strokeStyle)
        : this(PdfPaintStyle.Stroke, strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle)))
    {
    }

    private PdfPaint(PdfPaintStyle style, PdfStrokeStyle? strokeStyle)
        : this(
            style,
            PdfDeviceGrayColorSpaceConverter.Instance,
            PdfDeviceColorMapping.None,
            new PdfColor(PdfDeviceGrayColorSpaceConverter.Instance.GetInitialComponents()),
            1f,
            PdfRenderingIntent.RelativeColorimetric,
            true,
            null,
            PdfBlendMode.Normal,
            false,
            null,
            strokeStyle,
            null)
    {
    }

    /// <summary>
    /// Gets whether this paint is used for filling or stroking.
    /// </summary>
    public PdfPaintStyle Style { get; }

    /// <summary>
    /// Gets the color space <see cref="Color"/> is given in.
    /// </summary>
    public PdfColorSpaceConverter ColorSpace { get; }

    /// <summary>
    /// Gets the color spaces the device color spaces of <see cref="ColorSpace"/> are interpreted in.
    /// </summary>
    public PdfDeviceColorMapping DeviceColorMapping { get; }

    /// <summary>
    /// Gets the color components for solid color paints, or the tint of uncolored (stencil) patterns.
    /// </summary>
    public PdfColor Color { get; }

    /// <summary>
    /// Gets the alpha constant (ca for fill paints, CA for stroke paints).
    /// </summary>
    public float Alpha { get; }

    /// <summary>
    /// Gets the rendering intent the color is converted with.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Gets whether black point compensation is applied when the color is converted.
    /// </summary>
    public bool UseBlackPointCompensation { get; }

    /// <summary>
    /// Gets the transfer function (TR) applied to the converted color, or null when none applies.
    /// </summary>
    internal TransferFunctionTransform? TransferFunction { get; }

    /// <summary>
    /// Gets the resolved sRGB color, with <see cref="Alpha"/> as its alpha channel.
    /// </summary>
    public PdfRgbaColor RgbColor
        => ColorSpace.ToSrgb(Color.Components.Span, new PdfColorConversionParameters(RenderingIntent, TransferFunction, UseBlackPointCompensation, DeviceColorMapping)).WithAlpha(Alpha);

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
    /// Gets the mask compositing parameters (kind, subtype and transfer function) when this paint composites a
    /// previously-rendered mask layer onto content, a soft mask or the shape a pattern is painted within; null for every
    /// other paint.
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
    internal bool RequiresComposition => Alpha < 1 || BlendMode != PdfBlendMode.Normal;

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
    /// Returns a copy of this paint with <see cref="ColorSpace"/>, <see cref="DeviceColorMapping"/> and
    /// <see cref="Color"/> replaced, clearing any pattern.
    /// </summary>
    public PdfPaint WithSolidColor(PdfColorSpaceConverter colorSpace, PdfDeviceColorMapping deviceColorMapping, in PdfColor color)
    {
        if (colorSpace == null)
        {
            throw new ArgumentNullException(nameof(colorSpace));
        }

        if (deviceColorMapping == null)
        {
            throw new ArgumentNullException(nameof(deviceColorMapping));
        }

        return new PdfPaint(
            Style,
            colorSpace,
            deviceColorMapping,
            color,
            Alpha,
            RenderingIntent,
            UseBlackPointCompensation,
            TransferFunction,
            BlendMode,
            Overprint,
            null,
            StrokeStyle,
            MaskParameters);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="ColorSpace"/> and <see cref="DeviceColorMapping"/> replaced and no
    /// <see cref="Color"/>, for content that carries its own colors in <paramref name="colorSpace"/>.
    /// </summary>
    public PdfPaint WithColorSpace(PdfColorSpaceConverter colorSpace, PdfDeviceColorMapping deviceColorMapping)
    {
        if (colorSpace == null)
        {
            throw new ArgumentNullException(nameof(colorSpace));
        }

        if (deviceColorMapping == null)
        {
            throw new ArgumentNullException(nameof(deviceColorMapping));
        }

        return new PdfPaint(
            Style,
            colorSpace,
            deviceColorMapping,
            PdfColor.Empty,
            Alpha,
            RenderingIntent,
            UseBlackPointCompensation,
            TransferFunction,
            BlendMode,
            Overprint,
            Pattern,
            StrokeStyle,
            MaskParameters);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="Pattern"/> replaced and <see cref="Color"/> set to
    /// <paramref name="tintColor"/>, the tint of an uncolored pattern in the underlying color space of
    /// <see cref="ColorSpace"/>.
    /// </summary>
    public PdfPaint WithPattern(PdfPattern pattern, in PdfColor tintColor)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return new PdfPaint(
            Style,
            ColorSpace,
            DeviceColorMapping,
            tintColor,
            Alpha,
            RenderingIntent,
            UseBlackPointCompensation,
            TransferFunction,
            BlendMode,
            Overprint,
            pattern,
            StrokeStyle,
            MaskParameters);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="Alpha"/> replaced.
    /// </summary>
    public PdfPaint WithAlpha(float alpha)
        => new(Style, ColorSpace, DeviceColorMapping, Color, alpha, RenderingIntent, UseBlackPointCompensation, TransferFunction, BlendMode, Overprint, Pattern, StrokeStyle, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="BlendMode"/> replaced.
    /// </summary>
    public PdfPaint WithBlendMode(PdfBlendMode blendMode)
        => new(Style, ColorSpace, DeviceColorMapping, Color, Alpha, RenderingIntent, UseBlackPointCompensation, TransferFunction, blendMode, Overprint, Pattern, StrokeStyle, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="StrokeStyle"/> replaced. Only valid on stroke paints.
    /// </summary>
    public PdfPaint WithStrokeStyle(PdfStrokeStyle strokeStyle)
    {
        if (Style != PdfPaintStyle.Stroke)
        {
            throw new InvalidOperationException($"{nameof(WithStrokeStyle)} is only available on paints with {nameof(PdfPaintStyle)}.{nameof(PdfPaintStyle.Stroke)}.");
        }

        return new PdfPaint(
            Style,
            ColorSpace,
            DeviceColorMapping,
            Color,
            Alpha,
            RenderingIntent,
            UseBlackPointCompensation,
            TransferFunction,
            BlendMode,
            Overprint,
            Pattern,
            strokeStyle ?? throw new ArgumentNullException(nameof(strokeStyle)),
            MaskParameters);
    }

    /// <summary>
    /// Returns a copy of this paint with <see cref="MaskParameters"/> replaced.
    /// </summary>
    public PdfPaint WithMaskParameters(PdfMaskPaintParameters? maskParameters)
        => new(Style, ColorSpace, DeviceColorMapping, Color, Alpha, RenderingIntent, UseBlackPointCompensation, TransferFunction, BlendMode, Overprint, Pattern, StrokeStyle, maskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="RenderingIntent"/> replaced.
    /// </summary>
    internal PdfPaint WithRenderingIntent(PdfRenderingIntent renderingIntent)
        => new(Style, ColorSpace, DeviceColorMapping, Color, Alpha, renderingIntent, UseBlackPointCompensation, TransferFunction, BlendMode, Overprint, Pattern, StrokeStyle, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with <see cref="RenderingIntent"/>, <see cref="UseBlackPointCompensation"/> and
    /// <see cref="TransferFunction"/> replaced.
    /// </summary>
    internal PdfPaint WithColorConversion(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation, TransferFunctionTransform? transferFunction)
        => new(Style, ColorSpace, DeviceColorMapping, Color, Alpha, renderingIntent, useBlackPointCompensation, transferFunction, BlendMode, Overprint, Pattern, StrokeStyle, MaskParameters);

    /// <summary>
    /// Returns a copy of this paint with every field present in <paramref name="parameters"/> (from a <c>gs</c>
    /// ExtGState application) applied: alpha (ca for fill paints, CA for stroke paints), blend mode, overprint
    /// (op/OP), rendering intent, black point compensation, transfer function and stroke styling. Fields absent from
    /// <paramref name="parameters"/> are left unchanged.
    /// Returns this instance unchanged when nothing in <paramref name="parameters"/> applies.
    /// </summary>
    internal PdfPaint WithGraphicsState(PdfGraphicsStateParameters parameters)
    {
        bool isStroke = Style == PdfPaintStyle.Stroke;
        float? alpha = isStroke ? parameters.StrokeAlpha : parameters.FillAlpha;
        bool? overprint = isStroke ? parameters.OverprintStroke : parameters.OverprintFill;
        PdfStrokeStyle? strokeStyle = StrokeStyle?.WithGraphicsState(parameters);
        bool changesTransferFunction = parameters.TransferFunction != null || parameters.ShouldUnsetTransferFunction;

        if (alpha == null
            && overprint == null
            && parameters.BlendMode == null
            && parameters.PdfRenderingIntent == null
            && parameters.UseBlackPtComp == null
            && !changesTransferFunction
            && ReferenceEquals(strokeStyle, StrokeStyle))
        {
            return this;
        }

        bool useBlackPointCompensation = UseBlackPointCompensation;
        if (parameters.UseBlackPtComp != null)
        {
            useBlackPointCompensation = parameters.UseBlackPtComp.Value != PdfBlackPointCompensation.Off;
        }

        TransferFunctionTransform? transferFunction = TransferFunction;
        if (changesTransferFunction)
        {
            transferFunction = parameters.TransferFunction;
        }

        return new PdfPaint(
            Style,
            ColorSpace,
            DeviceColorMapping,
            Color,
            alpha ?? Alpha,
            parameters.PdfRenderingIntent ?? RenderingIntent,
            useBlackPointCompensation,
            transferFunction,
            parameters.BlendMode ?? BlendMode,
            overprint ?? Overprint,
            Pattern,
            strokeStyle,
            MaskParameters);
    }
}

using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Transparency;
using System;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Single entry point for constructing annotation fallback-rendering paints, so every annotation's
/// paint construction is easy to find in one place. Annotations with genuinely specialized styling
/// (a fixed blend mode, a computed color) get their own dedicated method; plain stroke paints
/// share the generic methods below.
/// </summary>
internal static class PdfAnnotationPaintFactory
{
    /// <summary>
    /// Resolves an annotation color array to a fill paint in the device color space its component count selects, with
    /// the device color mapping of <paramref name="page"/>.
    /// </summary>
    /// <param name="colorComponents">The color component array from the annotation.</param>
    /// <param name="page">The page the annotation is on.</param>
    /// <returns>The fill paint of the color, or null when there is no color or its component count selects no color space.</returns>
    public static PdfPaint? ResolvePaint(float[]? colorComponents, IPdfPageInternal page)
    {
        if (colorComponents == null || PdfColorSpaceResolver.ResolveDeviceConverter(colorComponents.Length) == null)
        {
            return null;
        }

        return CreatePaint(colorComponents, page.Resources.DeviceColorMapping);
    }

    /// <summary>
    /// Creates the fill paint of a fixed color of the fallback rendering in DeviceGray, DeviceRGB or DeviceCMYK by its
    /// component count.
    /// </summary>
    /// <param name="colorComponents">One, three or four color components.</param>
    public static PdfPaint CreatePaint(float[] colorComponents) => CreatePaint(colorComponents, PdfDeviceColorMapping.None);

    /// <summary>
    /// Creates the fill paint of a fixed DeviceRGB color of the fallback rendering, with its alpha.
    /// </summary>
    public static PdfPaint CreatePaint(in PdfRgbaColor color) => CreatePaint([color.Red, color.Green, color.Blue]).WithAlpha(color.Alpha);

    private static PdfPaint CreatePaint(float[] colorComponents, PdfDeviceColorMapping deviceColorMapping)
    {
        PdfColorSpaceConverter colorSpace = PdfColorSpaceResolver.ResolveDeviceConverter(colorComponents.Length)
            ?? throw new ArgumentException("An annotation color has one, three or four components.", nameof(colorComponents));

        return new PdfPaint(PdfPaintStyle.Fill).WithSolidColor(colorSpace, deviceColorMapping, new PdfColor(colorComponents));
    }

    /// <summary>
    /// Fill paint used for the translucent opacity layer wrapping fallback rendering (CA entry).
    /// </summary>
    public static PdfPaint CreateOpacityLayerPaint(float opacity)
        => PdfPaintFactory.WithGray(new PdfPaint(PdfPaintStyle.Fill), PdfPaintFactory.WhiteGray).WithAlpha(opacity);

    /// <summary>
    /// Fill paint for a Highlight annotation's marked region (Multiply blend so the mark darkens the text
    /// beneath it). Opacity rides on the paint rather than on a layer around it, which would isolate the
    /// blend from the page it has to darken.
    /// </summary>
    public static PdfPaint CreateHighlightPaint(PdfPaint paint, float opacity)
        => paint.WithAlpha(paint.Alpha * opacity).WithBlendMode(PdfBlendMode.Multiply);

    /// <summary>
    /// Fill paint that inverts the colors beneath it.
    /// </summary>
    public static PdfPaint CreateInvertFillPaint()
        => PdfPaintFactory.WithGray(new PdfPaint(PdfPaintStyle.Fill), PdfPaintFactory.WhiteGray).WithBlendMode(PdfBlendMode.Difference);

    /// <summary>
    /// Stroke paint of <paramref name="strokeStyle"/> that inverts the colors beneath it.
    /// </summary>
    public static PdfPaint CreateInvertStrokePaint(PdfStrokeStyle strokeStyle)
        => PdfPaintFactory.WithGray(new PdfPaint(strokeStyle), PdfPaintFactory.WhiteGray).WithBlendMode(PdfBlendMode.Difference);

    /// <summary>
    /// Plain stroke paint using default stroke styling (width 1, Butt cap, Miter join, no dash/border effect).
    /// </summary>
    public static PdfPaint CreateStrokePaint(PdfPaint paint) => CreateStrokePaint(paint, new PdfStrokeStyle());

    /// <summary>
    /// Plain stroke paint with a specific line width; otherwise default stroke styling
    /// (Butt cap, Miter join, no dash/border effect).
    /// </summary>
    public static PdfPaint CreateStrokePaint(PdfPaint paint, float lineWidth)
    {
        PdfStrokeStyle strokeStyle = new(lineWidth: lineWidth);
        return CreateStrokePaint(paint, strokeStyle);
    }

    /// <summary>
    /// Stroke paint from a fully resolved stroke style. Color is the only piece resolved at render time
    /// (it depends on the page's color space); width, cap, join, and dash are expected to already be
    /// set on <paramref name="strokeStyle"/>.
    /// </summary>
    public static PdfPaint CreateStrokePaint(PdfPaint paint, PdfStrokeStyle strokeStyle)
        => new PdfPaint(strokeStyle).WithSolidColor(paint.ColorSpace, paint.DeviceColorMapping, paint.Color).WithAlpha(paint.Alpha);

    /// <summary>
    /// Stroke paint from a fully resolved stroke style plus a parsed annotation border effect (BE entry).
    /// </summary>
    public static PdfPaint CreateStrokePaint(PdfPaint paint, PdfStrokeStyle strokeStyle, in PdfAnnotationBorderEffect borderEffect)
    {
        switch (borderEffect.Type)
        {
            case PdfAnnotationBorderEffectType.Cloudy when borderEffect.Intensity > 0f:
                return CreateStrokePaint(paint, strokeStyle.WithLineJoin(PdfStrokeJoin.Round));

            default:
                return CreateStrokePaint(paint, strokeStyle);
        }
    }
}

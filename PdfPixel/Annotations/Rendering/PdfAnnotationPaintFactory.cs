using PdfPixel.Annotations.Models;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Single entry point for constructing annotation fallback-rendering paints, so every annotation's
/// paint construction is easy to find in one place. Annotations with genuinely specialized styling
/// (a fixed blend mode, a computed color) get their own dedicated method; plain fill/stroke paints
/// share the generic methods below.
/// </summary>
internal static class PdfAnnotationPaintFactory
{
    /// <summary>
    /// Fill paint used for the translucent opacity layer wrapping fallback rendering (CA entry).
    /// </summary>
    public static PdfPaint CreateOpacityLayerPaint(float opacity) => new PdfPaint(PdfPaintStyle.Fill).WithSolidColor(PdfColors.White.WithAlpha(opacity));

    /// <summary>
    /// Fill paint for a Highlight annotation's marked region (Multiply blend so the mark darkens the text beneath it).
    /// </summary>
    public static PdfPaint CreateHighlightPaint(in PdfColor color)
        => new PdfPaint(PdfPaintStyle.Fill).WithSolidColor(color).WithBlendMode(PdfBlendMode.Multiply);

    /// <summary>
    /// Border stroke paint for a Stamp annotation's rounded-rectangle frame, with a drop-shadow effect
    /// that gives the stamp its embossed look.
    /// </summary>
    public static PdfPaint CreateStampBorderPaint(in PdfColor color, float borderWidth)
    {
        float shadowOffset = borderWidth * 0.5f;
        PdfPaintShadowEffect shadowEffect = new(shadowOffset, -shadowOffset, borderWidth * 0.3f, borderWidth * 0.3f, PdfColors.Black.WithAlpha(80f / 255f));
        PdfStrokeStyle strokeStyle = new(lineWidth: borderWidth);
        return new PdfPaint(strokeStyle).WithSolidColor(color).WithShadowEffect(shadowEffect);
    }

    /// <summary>
    /// Plain fill paint.
    /// </summary>
    public static PdfPaint CreateFillPaint(in PdfColor color) => new PdfPaint(PdfPaintStyle.Fill).WithSolidColor(color);

    /// <summary>
    /// Plain stroke paint using default stroke styling (width 1, Butt cap, Miter join, no dash/border effect).
    /// </summary>
    public static PdfPaint CreateStrokePaint(in PdfColor color) => new PdfPaint(PdfPaintStyle.Stroke).WithSolidColor(color);

    /// <summary>
    /// Plain stroke paint with a specific line width; otherwise default stroke styling
    /// (Butt cap, Miter join, no dash/border effect).
    /// </summary>
    public static PdfPaint CreateStrokePaint(in PdfColor color, float lineWidth)
    {
        PdfStrokeStyle strokeStyle = new(lineWidth: lineWidth);
        return CreateStrokePaint(color, strokeStyle);
    }

    /// <summary>
    /// Stroke paint from a fully resolved stroke style. Color is the only piece resolved at render time
    /// (it depends on the page's color space); width, cap, join, and dash are expected to already be
    /// set on <paramref name="strokeStyle"/>.
    /// </summary>
    public static PdfPaint CreateStrokePaint(in PdfColor color, PdfStrokeStyle strokeStyle) => new PdfPaint(strokeStyle).WithSolidColor(color);

    /// <summary>
    /// Stroke paint from a fully resolved stroke style plus a parsed annotation border effect (BE entry).
    /// </summary>
    public static PdfPaint CreateStrokePaint(in PdfColor color, PdfStrokeStyle strokeStyle, PdfAnnotationBorderEffect borderEffect)
    {
        PdfStrokeEffectType effectType = (borderEffect.Type == PdfAnnotationBorderEffectType.Cloudy)
            ? PdfStrokeEffectType.Cloudy
            : PdfStrokeEffectType.Solid;

        return CreateStrokePaint(color, strokeStyle.WithEffect(effectType, borderEffect.Intensity));
    }
}

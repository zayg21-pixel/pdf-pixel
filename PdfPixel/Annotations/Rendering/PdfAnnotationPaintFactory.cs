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
    public static PdfPaint CreateOpacityLayerPaint(float opacity) => new(PdfPaintStyle.Fill) { Color = PdfColors.White.WithAlpha(opacity) };

    /// <summary>
    /// Fill paint for a Highlight annotation's marked region (Multiply blend so the mark darkens the text beneath it).
    /// </summary>
    public static PdfPaint CreateHighlightPaint(in PdfColor color) => new(PdfPaintStyle.Fill) { Color = color, BlendMode = PdfBlendMode.Multiply };

    /// <summary>
    /// Border stroke paint for a Stamp annotation's rounded-rectangle frame, with the fixed Beveled
    /// drop-shadow styling that gives the stamp its embossed look.
    /// </summary>
    public static PdfPaint CreateStampBorderPaint(in PdfColor color, float borderWidth)
    {
        PdfStrokeStyle strokeStyle = new() { LineWidth = borderWidth, BorderStyleType = PdfBorderStyleType.Beveled };
        return new PdfPaint(strokeStyle) { Color = color };
    }

    /// <summary>
    /// Plain fill paint.
    /// </summary>
    public static PdfPaint CreateFillPaint(in PdfColor color) => new(PdfPaintStyle.Fill) { Color = color };

    /// <summary>
    /// Plain stroke paint using default stroke styling (width 1, Butt cap, Miter join, no dash/border effect).
    /// </summary>
    public static PdfPaint CreateStrokePaint(in PdfColor color) => new(PdfPaintStyle.Stroke) { Color = color };

    /// <summary>
    /// Stroke paint from a fully resolved stroke style. Color is the only piece resolved at render time
    /// (it depends on the page's color space); width, cap, join, dash, and border style/effect are
    /// expected to already be set on <paramref name="strokeStyle"/>.
    /// </summary>
    public static PdfPaint CreateStrokePaint(in PdfColor color, PdfStrokeStyle strokeStyle) => new(strokeStyle) { Color = color };
}

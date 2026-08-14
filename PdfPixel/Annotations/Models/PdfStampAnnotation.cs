using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF rubber stamp annotation.
/// </summary>
/// <remarks>
/// Stamp annotations display a predefined icon or text label (e.g. "DRAFT", "APPROVED")
/// as a visual mark on the page. When no appearance stream is available, the fallback
/// renders a rounded rectangle border with centered label text. Both the border and text
/// carry a drop-shadow paint filter to imitate a physical stamp impression.
/// </remarks>
public class PdfStampAnnotation : PdfAnnotationBase
{
    private const float BorderWidthDefault = 2f;
    private const float CornerRadiusFraction = 0.12f;
    private const float MarginFraction = 0.10f;
    private const byte ShadowAlpha = 80;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfStampAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this stamp annotation.</param>
    public PdfStampAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Stamp)
    {
        StampName = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.NameKey).AsEnum<PdfStampName>();
    }

    /// <summary>
    /// Gets the standard stamp name identifying the stamp type (e.g. Draft, Approved).
    /// </summary>
    public PdfStampName StampName { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfColor stampColor = ResolveColor(page, new PdfColor(180f / 255f, 0f, 0f));
        float opacity = (visualStateKind == PdfAnnotationVisualStateKind.Rollover) ? 0.75f : 1.0f;
        PdfColor color = stampColor.WithAlpha(stampColor.Alpha * opacity);

        float borderWidth = (BorderStyle?.StrokeStyle.LineWidth > 0) ? BorderStyle.StrokeStyle.LineWidth : BorderWidthDefault;
        float cornerRadius = Math.Min(Rectangle.Width, Rectangle.Height) * CornerRadiusFraction;
        float halfBorderWidth = borderWidth / 2f;
        PdfRectangle borderRect = new(
            Rectangle.Left + halfBorderWidth,
            Rectangle.Top + halfBorderWidth,
            Rectangle.Right - halfBorderWidth,
            Rectangle.Bottom - halfBorderWidth);

        DrawBorder(processor, borderRect, cornerRadius, borderWidth, color);
        DrawLabel(processor, page, GetLabelText(StampName, Name), color);

        return true;
    }

    private static void DrawBorder(
        IPdfCommandProcessor processor,
        in PdfRectangle borderRect,
        float cornerRadius,
        float borderWidth,
        in PdfColor color)
    {
        PdfPaint borderPaint = PdfAnnotationPaintFactory.CreateStampBorderPaint(color, borderWidth);

        PdfPathBuilder borderPath = new();
        borderPath.AddRoundRect(borderRect, cornerRadius, cornerRadius);
        processor.Process(new DrawPathCommand(borderPath.ToPath(), borderPaint));
    }

    private void DrawLabel(IPdfCommandProcessor processor, IPdfPageInternal page, string labelText, in PdfColor color)
    {
        PdfSubstitutionInfo substitutionInfo = new(Fonts.Mapping.PdfStandardFontName.Courier, PdfSubstitutionInfo.BoldWeight, PdfSubstitutionInfo.NormalWidth, italicAngle: 0f);
        SfntPdfTypeface typeface = page.Document.FontProvider.GetTypefaceByUnicode(substitutionInfo, labelText);

        float availableWidth = Rectangle.Width * (1f - 2f * MarginFraction);
        float availableHeight = Rectangle.Height * (1f - 2f * MarginFraction);

        PdfFontMetrics metrics = typeface.Metrics;

        float glyphHeight = metrics.Ascent - metrics.Descent;
        float fontSize = (glyphHeight > 0f) ? availableHeight / glyphHeight : availableHeight;

        float textWidth = typeface.GetWidth(labelText) * fontSize;
        if (textWidth > availableWidth)
        {
            fontSize *= availableWidth / textWidth;
            textWidth = availableWidth;
        }

        float scaledAscent = metrics.Ascent * fontSize;
        float scaledDescent = metrics.Descent * fontSize;

        float textX = Rectangle.MidX - textWidth / 2f;
        float textY = Rectangle.MidY - (scaledAscent + scaledDescent) / 2f;

        // Shaped glyph positions are em-relative, so the font size scales them through the matrix.
        PdfMatrix textMatrix = PdfMatrix.Concat(
            PdfMatrix.CreateTranslation(textX, textY),
            PdfMatrix.CreateScale(fontSize, -fontSize));

        float shadowOffset = fontSize * 0.05f;
        float shadowSigma = fontSize * 0.03f;
        PdfPaintShadowEffect shadowEffect = new(shadowOffset, shadowOffset, shadowSigma, shadowSigma, PdfColors.Black.WithAlpha(ShadowAlpha / 255f));
        PdfPaint textPaint = new PdfPaint(PdfPaintStyle.Fill).WithSolidColor(color).WithShadowEffect(shadowEffect);

        List<ShapedGlyph> glyphs = [];
        ShapedGlyphBuilder.BuildFromText(labelText, typeface, glyphs);

        processor.Process(new DrawShapedTextCommand(textMatrix, glyphs.ToArray(), textPaint));
    }

    private static string GetLabelText(PdfStampName stampName, PdfString? rawName)
    {
        return stampName switch
        {
            PdfStampName.Approved => "APPROVED",
            PdfStampName.AsIs => "AS IS",
            PdfStampName.Confidential => "CONFIDENTIAL",
            PdfStampName.Departmental => "DEPARTMENTAL",
            PdfStampName.Draft => "DRAFT",
            PdfStampName.Experimental => "EXPERIMENTAL",
            PdfStampName.Expired => "EXPIRED",
            PdfStampName.Final => "FINAL",
            PdfStampName.ForComment => "FOR COMMENT",
            PdfStampName.ForPublicRelease => "FOR PUBLIC RELEASE",
            PdfStampName.NotApproved => "NOT APPROVED",
            PdfStampName.NotForPublicRelease => "NOT FOR PUBLIC RELEASE",
            PdfStampName.Sold => "SOLD",
            PdfStampName.TopSecret => "TOP SECRET",
            _ => (rawName == null || rawName.Value.IsEmpty) ? "STAMP" : rawName.Value.ToString().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Returns a string representation of this stamp annotation.
    /// </summary>
    public override string ToString() => $"Stamp Annotation ({StampName})";
}

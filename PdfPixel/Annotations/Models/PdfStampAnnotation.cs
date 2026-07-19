using PdfPixel.Commands;
using PdfPixel.Fonts.Management;
using PdfPixel.Models;
using PdfPixel.Path;
using PdfPixel.Text;
using SkiaSharp;
using System;

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
        StampName = annotationObject.Dictionary.GetName(PdfTokens.NameKey).AsEnum<PdfStampName>();
    }

    /// <summary>
    /// Gets the standard stamp name identifying the stamp type (e.g. Draft, Approved).
    /// </summary>
    public PdfStampName StampName { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        SKColor stampColor = ResolveColor(page, new SKColor(180, 0, 0));
        float opacity = (visualStateKind == PdfAnnotationVisualStateKind.Rollover) ? 0.75f : 1.0f;
        SKColor color = stampColor.WithAlpha((byte)(stampColor.Alpha * opacity));

        float borderWidth = (BorderStyle?.Width > 0) ? BorderStyle.Width : BorderWidthDefault;
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
        in SKColor color)
    {
        float shadowOffset = borderWidth * 0.5f;
        float shadowSigma = borderWidth * 0.3f;

        SKPaint borderPaint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            Color = color,
            ImageFilter = SKImageFilter.CreateDropShadow(shadowOffset, -shadowOffset, shadowSigma, shadowSigma, SKColors.Black.WithAlpha(ShadowAlpha))
        };

        PdfPath borderPath = new();
        borderPath.AddRoundRect(borderRect, cornerRadius, cornerRadius);
        processor.Process(new DrawPathCommand(borderPath, borderPaint));
    }

    private void DrawLabel(IPdfCommandProcessor processor, IPdfPageInternal page, string labelText, in SKColor color)
    {
        var substitutionInfo = PdfSubstitutionInfo.Parse(PdfString.FromString("Courier-Bold"), null);
        SKTypeface typeface = page.Document.FontSubstitutor.SubstituteTypeface(substitutionInfo, labelText, null);

        float availableWidth = Rectangle.Width * (1f - 2f * MarginFraction);
        float availableHeight = Rectangle.Height * (1f - 2f * MarginFraction);

        SKFont font = new(typeface, availableHeight);
        font.GetFontMetrics(out SKFontMetrics metrics);

        float glyphHeight = metrics.Descent - metrics.Ascent;
        if (glyphHeight > 0f)
        {
            font.Size *= availableHeight / glyphHeight;
        }

        float textWidth = font.MeasureText(labelText);
        if (textWidth > availableWidth)
        {
            font.Size *= availableWidth / textWidth;
            textWidth = availableWidth;
        }

        font.GetFontMetrics(out metrics);

        float textX = Rectangle.MidX - textWidth / 2f;
        float textY = Rectangle.MidY + (metrics.Ascent + metrics.Descent) / 2f;

        SKMatrix textMatrix = SKMatrix.Concat(
            SKMatrix.CreateTranslation(textX, textY),
            SKMatrix.CreateScale(1f, -1f));

        float shadowOffset = font.Size * 0.05f;
        float shadowSigma = font.Size * 0.03f;

        SKPaint textPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = color,
            ImageFilter = SKImageFilter.CreateDropShadow(shadowOffset, shadowOffset, shadowSigma, shadowSigma, SKColors.Black.WithAlpha(ShadowAlpha))
        };

        processor.Process(new DrawTextCommand(labelText, textMatrix, font, textPaint));
    }

    private static string GetLabelText(PdfStampName stampName, in PdfString rawName)
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
            _ => (rawName.IsEmpty) ? "STAMP" : rawName.ToString().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Returns a string representation of this stamp annotation.
    /// </summary>
    public override string ToString() => $"Stamp Annotation ({StampName})";
}

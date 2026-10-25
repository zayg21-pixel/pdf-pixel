using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering.Text;
using PdfPixel.Text;
using System;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF rubber stamp annotation.
/// </summary>
/// <remarks>
/// Stamp annotations display a predefined icon or text label (e.g. "DRAFT", "APPROVED")
/// as a visual mark on the page. When no appearance stream is available, the fallback
/// renders a slightly tilted rounded rectangle border with centered label text, both cast onto
/// a flat shadow offset to the bottom right to imitate a physical stamp impression.
/// </remarks>
public class PdfStampAnnotation : PdfMarkupAnnotation
{
    private const float BorderWidthFraction = 0.08f;
    private const float CornerRadiusFraction = 0.12f;
    private const float LabelWidthFraction = 0.8f;
    private const float LabelHeightFraction = 0.50f;
    private const float ShadowGray = 0.8f;
    private const float TiltDegrees = 1.25f;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfStampAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this stamp annotation.</param>
    internal PdfStampAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Stamp)
    {
        PdfString? stampName = annotationObject.Dictionary.GetName(PdfTokens.NameKey);
        StampName = stampName?.AsEnum<PdfStampName>() ?? PdfStampName.Draft;
        RawStampName = (StampName == PdfStampName.Raw) ? stampName : null;
    }

    /// <summary>
    /// Gets the standard stamp name identifying the stamp type (e.g. Draft, Approved).
    /// Default <see cref="PdfStampName.Draft"/>.
    /// </summary>
    public PdfStampName StampName { get; }

    /// <summary>
    /// Name as written when <see cref="StampName"/> is <see cref="PdfStampName.Raw"/>, otherwise <see langword="null"/>.
    /// </summary>
    public PdfString? RawStampName { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        bool isRubberStamp = Intent == PdfAnnotationIntent.None || Intent == PdfAnnotationIntent.Stamp;
        string? labelText = isRubberStamp ? GetLabelText() : null;

        if (labelText == null)
        {
            return false;
        }

        float opacity = (visualStateKind == PdfAnnotationVisualStateKind.Rollover) ? 0.75f : 1.0f;
        PdfColor color = GetStampColor().WithAlpha(opacity);
        PdfColor shadowColor = new PdfColor(ShadowGray, ShadowGray, ShadowGray).WithAlpha(color.Alpha);

        float shortSide = Math.Min(Rectangle.Width, Rectangle.Height);
        float borderWidth = shortSide * BorderWidthFraction;
        float cornerRadius = shortSide * CornerRadiusFraction;
        float halfBorderWidth = borderWidth / 2f;
        PdfRectangle borderRect = Rectangle.Inflate(-halfBorderWidth);

        PdfMatrix shadowMatrix = PdfMatrix.CreateTranslation(halfBorderWidth, -halfBorderWidth);
        PdfRectangle stampBounds = PdfRectangle.Union(Rectangle, shadowMatrix.MapRect(Rectangle));
        PdfMatrix tiltMatrix = CreateTiltMatrix(stampBounds, out float tiltScale);
        PdfMatrix shadowTiltMatrix = PdfMatrix.Concat(tiltMatrix, shadowMatrix);
        float tiltedBorderWidth = borderWidth * tiltScale;

        PdfPathBuilder borderPathBuilder = new();
        borderPathBuilder.AddRoundRect(borderRect, cornerRadius, cornerRadius);
        PdfPath borderPath = borderPathBuilder.ToPath();
        PdfPath? labelPath = CreateLabelPath(page, labelText);

        PdfPaint borderShadowPaint = PdfAnnotationPaintFactory.CreateStrokePaint(shadowColor, tiltedBorderWidth);
        processor.Process(new DrawPathCommand(borderPath.Transform(shadowTiltMatrix), borderShadowPaint));

        if (labelPath != null)
        {
            PdfPaint labelShadowPaint = PdfAnnotationPaintFactory.CreateFillPaint(shadowColor);
            processor.Process(new DrawPathCommand(labelPath.Transform(shadowTiltMatrix), labelShadowPaint));
        }

        PdfPaint borderPaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, tiltedBorderWidth);
        processor.Process(new DrawPathCommand(borderPath.Transform(tiltMatrix), borderPaint));

        if (labelPath != null)
        {
            PdfPaint labelPaint = PdfAnnotationPaintFactory.CreateFillPaint(color);
            processor.Process(new DrawPathCommand(labelPath.Transform(tiltMatrix), labelPaint));
        }

        return true;
    }

    private PdfMatrix CreateTiltMatrix(in PdfRectangle stampBounds, out float tiltScale)
    {
        PdfMatrix rotation = PdfMatrix.CreateRotationDegrees(TiltDegrees);
        PdfRectangle tiltedBounds = rotation.MapRect(stampBounds);
        tiltScale = Math.Min(Rectangle.Width / tiltedBounds.Width, Rectangle.Height / tiltedBounds.Height);

        PdfMatrix toOrigin = PdfMatrix.CreateTranslation(-stampBounds.MidX, -stampBounds.MidY);
        PdfMatrix scaledRotation = PdfMatrix.Concat(PdfMatrix.CreateScale(tiltScale, tiltScale), rotation);
        PdfMatrix toRectangle = PdfMatrix.CreateTranslation(Rectangle.MidX, Rectangle.MidY);

        return PdfMatrix.Concat(toRectangle, PdfMatrix.Concat(scaledRotation, toOrigin));
    }

    private PdfPath? CreateLabelPath(IPdfPageInternal page, string labelText)
    {
        PdfSubstitutionInfo substitutionInfo = new(PdfStandardFontName.Times, PdfSubstitutionInfo.BoldWeight, PdfSubstitutionInfo.NormalWidth, italicAngle: 0f);
        SfntPdfTypeface typeface = page.Document.FontProvider.GetTypefaceByUnicode(substitutionInfo, labelText);

        ShapedGlyphBuffer glyphs = new();
        ShapedGlyphBuilder.BuildFromText(labelText, typeface, glyphs);

        PdfPath emPath = TextRenderUtilities.GetTextPath(glyphs.Glyphs, PdfMatrix.CreateScale(1f, -1f));
        PdfRectangle inkBounds = emPath.GetBounds();

        if (inkBounds.Width <= 0f || inkBounds.Height <= 0f)
        {
            return null;
        }

        float fontSize = Math.Min(
            Rectangle.Width * LabelWidthFraction / inkBounds.Width,
            Rectangle.Height * LabelHeightFraction / inkBounds.Height);

        PdfMatrix labelMatrix = PdfMatrix.CreateScaleTranslation(
            fontSize,
            fontSize,
            Rectangle.MidX - (inkBounds.MidX * fontSize),
            Rectangle.MidY - (inkBounds.MidY * fontSize));

        return emPath.Transform(labelMatrix);
    }

    private PdfColor GetStampColor()
    {
        switch (StampName)
        {
            case PdfStampName.Approved:
            case PdfStampName.ForComment:
            case PdfStampName.ForPublicRelease:
                return new PdfColor(70f / 255f, 160f / 255f, 70f / 255f);

            case PdfStampName.Departmental:
            case PdfStampName.Experimental:
            case PdfStampName.Sold:
                return new PdfColor(0f, 113f / 255f, 187f / 255f);

            default:
                return new PdfColor(240f / 255f, 63f / 255f, 35f / 255f);
        }
    }

    private string? GetLabelText()
    {
        return StampName switch
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
            _ => null
        };
    }

    /// <summary>
    /// Returns a string representation of this stamp annotation.
    /// </summary>
    public override string ToString() => $"Stamp Annotation ({StampName})";
}

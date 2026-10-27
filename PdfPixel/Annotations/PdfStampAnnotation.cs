using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.Text;
using PdfPixel.Text;
using System;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF rubber stamp annotation.
/// </summary>
public class PdfStampAnnotation : PdfMarkupAnnotation
{
    private const float BorderWidthFraction = 0.08f;
    private const float CornerRadiusFraction = 0.12f;
    private const float LabelWidthFraction = 0.8f;
    private const float LabelHeightFraction = 0.50f;
    private const float ShadowGray = 0.8f;
    private const float TiltDegrees = 1.25f;

    private static readonly PdfSubstitutionInfo LabelFont = new(
        PdfStandardFontName.Times,
        PdfSubstitutionInfo.BoldWeight,
        PdfSubstitutionInfo.NormalWidth,
        italicAngle: 0f);

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

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
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

        SfntPdfTypeface labelTypeface = page.Document.FontProvider.GetTypefaceByUnicode(LabelFont, labelText);
        ShapedGlyph[] labelGlyphs = PdfAnnotationTextRenderer.Shape(labelText, labelTypeface);

        DrawImpression(processor, borderPath, labelGlyphs, labelTypeface.Metrics, shadowTiltMatrix, tiltedBorderWidth, shadowColor);
        DrawImpression(processor, borderPath, labelGlyphs, labelTypeface.Metrics, tiltMatrix, tiltedBorderWidth, color);

        return true;
    }

    /// <summary>
    /// Draws the border and the label centered in the rectangle, mapped through <paramref name="matrix"/>.
    /// </summary>
    private void DrawImpression(
        IPdfCommandProcessor processor,
        PdfPath borderPath,
        ShapedGlyph[] labelGlyphs,
        PdfFontMetrics labelMetrics,
        in PdfMatrix matrix,
        float borderWidth,
        in PdfColor color)
    {
        PdfPaint borderPaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, borderWidth);
        processor.Process(new DrawPathCommand(borderPath.Transform(matrix), borderPaint));

        float labelWidth = TextRenderUtilities.GetTextWidth(labelGlyphs);
        float capHeight = labelMetrics.CapHeight;

        if (labelWidth <= 0f || capHeight <= 0f)
        {
            return;
        }

        float fontSize = Math.Min(
            Rectangle.Width * LabelWidthFraction / labelWidth,
            Rectangle.Height * LabelHeightFraction / capHeight);

        PdfPoint labelOrigin = new(
            Rectangle.MidX - (labelWidth * fontSize / 2f),
            Rectangle.MidY - (capHeight * fontSize / 2f));

        PdfAnnotationTextRenderer.DrawText(processor, labelGlyphs, matrix, labelOrigin, fontSize, color);
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

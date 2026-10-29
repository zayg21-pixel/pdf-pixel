using PdfPixel.Actions;
using PdfPixel.Annotations.Icons;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Files;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF file attachment annotation.
/// </summary>
public class PdfFileAttachmentAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileAttachmentAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this file attachment annotation.</param>
    internal PdfFileAttachmentAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FileAttachment)
    {
        FileSpecification = PdfFileSpecification.GetFileSpecification(annotationObject.Dictionary, PdfTokens.FSKey);
        Icon = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.NameKey).AsEnum<PdfFileAttachmentIcon>();

        if (FileSpecification != null)
        {
            Action = new PdfLaunchAction(FileSpecification);
        }
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Gets the icon rectangle grown by <paramref name="tolerance"/>.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance)
        => PdfAnnotationGeometry.CreateRectanglePath(Rectangle.Inflate(tolerance), borderStyle: null);

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <summary>
    /// File specification of the attached file (FS), or <see langword="null"/> when absent.
    /// </summary>
    public PdfFileSpecification? FileSpecification { get; }

    /// <summary>
    /// Launch action that opens <see cref="FileSpecification"/>, or <see langword="null"/> when the annotation has no file.
    /// </summary>
    public PdfLaunchAction? Action { get; }

    /// <summary>
    /// The icon type that should be used to display this file attachment.
    /// </summary>
    public PdfFileAttachmentIcon Icon { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        string iconName = (Icon == PdfFileAttachmentIcon.Unknown)
            ? nameof(PdfFileAttachmentIcon.PushPin)
            : Icon.ToString();

        PdfAnnotationIconDefinition? iconDefinition = PdfAnnotationGraphics.GetAnnotationIcon(iconName, visualStateKind);

        if (iconDefinition == null)
        {
            return false;
        }

        PdfAnnotationGraphics.RenderIcon(processor, iconDefinition, Rectangle, PdfColors.Black, ResolveColor(page));
        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        PdfString? fileName = FileSpecification?.UnicodeFile ?? FileSpecification?.File;
        if (fileName != null)
        {
            return $"FileAttachment: {fileName.Value.DecodePdfString()}";
        }

        return "FileAttachment";
    }
}

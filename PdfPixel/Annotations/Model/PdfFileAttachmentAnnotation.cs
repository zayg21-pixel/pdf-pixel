using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Commands.Model;
using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Model;

/// <summary>
/// Represents a PDF file attachment annotation.
/// </summary>
/// <remarks>
/// File attachment annotations (FileAttachment) reference a file specification (Filespec) which
/// contains an embedded file stream in the /EF dictionary. This class exposes basic metadata
/// about the attached file and provides a minimal fallback rendering (paperclip icon + name).
/// </remarks>
public class PdfFileAttachmentAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileAttachmentAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this file attachment annotation.</param>
    public PdfFileAttachmentAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FileAttachment)
    {
        FileSpecification = PdfFileSpecification.FromDictionaryEntry(annotationObject.Dictionary, PdfTokens.FSKey);
        Icon = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.NameKey).AsEnum<PdfFileAttachmentIcon>();
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <summary>
    /// File specification of the attached file (FS), or <see langword="null"/> when absent.
    /// </summary>
    public PdfFileSpecification? FileSpecification { get; }

    /// <summary>
    /// The icon type that should be used to display this file attachment.
    /// </summary>
    public PdfFileAttachmentIcon Icon { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        string iconName = (Icon == PdfFileAttachmentIcon.Unknown)
            ? nameof(PdfFileAttachmentIcon.PushPin)
            : Icon.ToString();

        PdfAnnotationIconDefinition? iconDefinition = PdfAnnotationGraphics.GetAnnotationIcon(iconName, visualStateKind);

        if (iconDefinition == null)
        {
            return false;
        }

        PdfColor color = ResolveColor(page, PdfColors.DarkBlue);
        PdfAnnotationGraphics.RenderIcon(processor, iconDefinition, Rectangle, color, null);
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

using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

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
        // Filespec can be in the /FS entry (PDF spec) or in the /F string key for older usage.
        FileSpec = annotationObject.Dictionary.GetDictionary(PdfTokens.FSKey) ?? annotationObject.Dictionary.GetDictionary(PdfTokens.FKey);

        if (FileSpec != null)
        {
            FileName = FileSpec.GetString(PdfTokens.FKey);

            // Embedded file dictionary is in /EF with key /F or /UF. Try both.
            PdfDictionary? efDict = FileSpec.GetDictionary(PdfTokens.EFKey);
            if (efDict != null)
            {
                EmbeddedFileObject = efDict.GetObject(PdfTokens.FKey) ?? efDict.GetObject(PdfTokens.UFKey);
            }

            // Alternatively some filespecs place the file stream directly in the Filespec as /EF
            EmbeddedFileObject ??= FileSpec.GetObject(PdfTokens.EFKey);
        }

        Icon = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.NameKey).AsEnum<PdfFileAttachmentIcon>();

        // TODO: [LOW] complete FileSpec object parsing
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <summary>
    /// The filespec dictionary describing the attached file.
    /// </summary>
    public PdfDictionary? FileSpec { get; }

    /// <summary>
    /// The icon type that should be used to display this file attachment.
    /// </summary>
    public PdfFileAttachmentIcon Icon { get; }

    /// <summary>
    /// The original file name of the attached file, if present.
    /// </summary>
    public PdfString? FileName { get; }

    /// <summary>
    /// The PDF object that contains the embedded file stream, if available.
    /// </summary>
    public PdfObject? EmbeddedFileObject { get; }

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
        if (FileName?.IsEmpty == false)
        {
            return $"FileAttachment: {FileName}";
        }

        return "FileAttachment";
    }
}

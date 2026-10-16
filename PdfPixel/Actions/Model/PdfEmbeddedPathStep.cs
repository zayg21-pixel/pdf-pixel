using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// One step of the path of an embedded go-to action (a target dictionary), as written.
/// </summary>
public sealed class PdfEmbeddedPathStep
{
    internal PdfEmbeddedPathStep(PdfDictionary targetDictionary)
    {
        Direction = targetDictionary.GetNameOrDefault(PdfTokens.TargetRelationshipKey).AsEnum<PdfEmbeddedPathDirection>();
        EmbeddedFileName = targetDictionary.GetString(PdfTokens.TargetFileNameKey);
        AnnotationPageIndex = targetDictionary.GetInteger(PdfTokens.TargetPageKey);
        AnnotationPageDestination = targetDictionary.GetString(PdfTokens.TargetPageKey);
        AnnotationIndex = targetDictionary.GetInteger(PdfTokens.TargetAnnotationKey);
        AnnotationName = targetDictionary.GetString(PdfTokens.TargetAnnotationKey);
    }

    /// <summary>
    /// Direction of the step (/R).
    /// </summary>
    public PdfEmbeddedPathDirection Direction { get; }

    /// <summary>
    /// Name of the child in the EmbeddedFiles name tree (/N), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? EmbeddedFileName { get; }

    /// <summary>
    /// Zero-based index of the page holding the file attachment annotation of the child (/P written as an
    /// integer), or <see langword="null"/> when absent.
    /// </summary>
    public int? AnnotationPageIndex { get; }

    /// <summary>
    /// Name of the named destination giving the page that holds the file attachment annotation of the child
    /// (/P written as a string), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? AnnotationPageDestination { get; }

    /// <summary>
    /// Zero-based index of the file attachment annotation of the child in the page's annotations (/A written
    /// as an integer), or <see langword="null"/> when absent.
    /// </summary>
    public int? AnnotationIndex { get; }

    /// <summary>
    /// Name (/NM) of the file attachment annotation of the child (/A written as a string), or
    /// <see langword="null"/> when absent.
    /// </summary>
    public PdfString? AnnotationName { get; }
}

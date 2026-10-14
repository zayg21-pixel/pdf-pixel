using PdfPixel.Content.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Tagging.Model;

namespace PdfPixel.Content;

/// <summary>
/// Helpers shared by the extractors of a content tree.
/// </summary>
internal static class PdfContentExtractionUtilities
{
    /// <summary>
    /// Returns whether <paramref name="scope"/> is a content stream excluded by <paramref name="parameters"/>.
    /// </summary>
    public static bool IsExcludedContentStream(PdfScope? scope, PdfContentExtractionParameters parameters)
    {
        if (scope is not PdfContentStreamScope contentStream)
        {
            return false;
        }

        return contentStream.StreamType switch
        {
            PdfContentStreamType.Type3Glyph => !parameters.IncludeType3Content,
            PdfContentStreamType.SoftMask => !parameters.IncludeSoftMaskContent,
            _ => false
        };
    }

    /// <summary>
    /// Returns the matrix mapping the content of <paramref name="item"/> into the space of its content tree.
    /// </summary>
    public static PdfMatrix GetContentMatrix(PdfContentItem item) => item.Matrix.PreConcat(item.Command.Matrix);

    /// <summary>
    /// Returns the alternate description (/Alt) of <paramref name="scope"/> or of its structure element,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public static PdfString? GetAlternateDescription(PdfContentScope scope)
    {
        if (scope is PdfMarkedContentScope markedContent && markedContent.AlternateDescription != null)
        {
            return markedContent.AlternateDescription;
        }

        return scope.StructureElement?.AlternateDescription;
    }

    /// <summary>
    /// Returns the replacement text (/ActualText) of <paramref name="scope"/> or of its structure element,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public static PdfString? GetActualText(PdfContentScope scope)
    {
        if (scope is PdfMarkedContentScope markedContent && markedContent.ActualText != null)
        {
            return markedContent.ActualText;
        }

        return scope.StructureElement?.ActualText;
    }
}

using PdfPixel.Models;
using PdfPixel.Tagging.Model;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Text-related properties extracted from a marked content scope.
/// </summary>
public class PdfTextMarkup
{
    /// <summary>
    /// Initializes a new <see cref="PdfTextMarkup"/> with the specified tag.
    /// </summary>
    public PdfTextMarkup(PdfTextTag tag)
    {
        Tag = tag;
        IsArtifact = tag == PdfTextTag.Artifact;
    }

    /// <summary>
    /// Standard structure tag, or <see cref="PdfTextTag.Custom"/> for non-standard tags.
    /// </summary>
    public PdfTextTag Tag { get; }

    /// <summary>
    /// Raw tag name when <see cref="Tag"/> is <see cref="PdfTextTag.Custom"/>.
    /// </summary>
    public PdfString? CustomTag { get; internal set; }

    /// <summary>
    /// Replacement text for ligatures or decorative glyphs (/ActualText), falling back to that of
    /// <see cref="StructureElement"/>.
    /// </summary>
    public PdfString? ActualText { get; internal set; }

    /// <summary>
    /// Alternate description (/Alt), falling back to that of <see cref="StructureElement"/>.
    /// </summary>
    public PdfString? Alt { get; internal set; }

    /// <summary>
    /// Expansion of an abbreviation or acronym (/E), falling back to that of <see cref="StructureElement"/>.
    /// </summary>
    public PdfString? ExpandedForm { get; internal set; }

    /// <summary>
    /// Language tag (/Lang), falling back to that of <see cref="StructureElement"/>.
    /// </summary>
    public PdfString? Lang { get; internal set; }

    /// <summary>
    /// Whether this scope is an artifact (non-logical content to exclude from selection).
    /// </summary>
    public bool IsArtifact { get; }

    /// <summary>
    /// Property list of the artifact, or <see langword="null"/> when this scope is not an artifact or
    /// has no property list.
    /// </summary>
    public PdfArtifactProperties? Artifact { get; internal set; }

    /// <summary>
    /// Marked content identifier (/MCID), or <see langword="null"/> when absent.
    /// </summary>
    public int? Mcid { get; internal set; }

    /// <summary>
    /// Structure element holding the marked content (/MCID), or <see langword="null"/> when not linked to the structure tree.
    /// </summary>
    public PdfStructureElement? StructureElement { get; internal set; }
}

using PdfPixel.Models;

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
    public PdfString CustomTag { get; internal set; }

    /// <summary>
    /// Replacement text for ligatures or decorative glyphs (/ActualText).
    /// </summary>
    public PdfString ActualText { get; internal set; }

    /// <summary>
    /// Language tag (/Lang).
    /// </summary>
    public PdfString Lang { get; internal set; }

    /// <summary>
    /// Whether this scope is an artifact (non-logical content to exclude from selection).
    /// </summary>
    public bool IsArtifact { get; }

    /// <summary>
    /// Marked content identifier linking to the structure tree (/MCID).
    /// </summary>
    public int? Mcid { get; internal set; }
}

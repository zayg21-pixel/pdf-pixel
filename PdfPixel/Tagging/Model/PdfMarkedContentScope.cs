using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// A marked content sequence (BMC, BDC) or point (MP, DP) with its tag and properties.
/// </summary>
public sealed class PdfMarkedContentScope : PdfContentScope
{
    /// <summary>
    /// Initializes a new <see cref="PdfMarkedContentScope"/> with the tag operand, optional content membership,
    /// associated files and structure element of the marked content.
    /// </summary>
    internal PdfMarkedContentScope(
        in PdfString tagName,
        PdfMarkedContentTag tagType,
        PdfOptionalContentMembership? optionalContent,
        IReadOnlyList<PdfFileSpecification>? associatedFiles,
        PdfStructureElement? structureElement)
        : base(optionalContent, associatedFiles, structureElement)
    {
        TagName = tagName;
        TagType = tagType;
    }

    /// <summary>
    /// Tag operand as written in the content stream (e.g. "OC", "Span", "Artifact").
    /// </summary>
    public PdfString TagName { get; }

    /// <summary>
    /// Tag operand, or <see cref="PdfMarkedContentTag.Raw"/> when the tag is not defined by a specification.
    /// </summary>
    public PdfMarkedContentTag TagType { get; }

    /// <summary>
    /// Marked content identifier (/MCID), or <see langword="null"/> when absent.
    /// </summary>
    public int? Mcid { get; internal set; }

    /// <summary>
    /// Whether the marked content is an artifact (Artifact tag).
    /// </summary>
    public bool IsArtifact => TagType == PdfMarkedContentTag.Artifact;

    /// <summary>
    /// Property list of the artifact, or <see langword="null"/> when the content is not an artifact or
    /// the artifact has no property list.
    /// </summary>
    public PdfArtifactProperties? Artifact { get; internal set; }

    /// <summary>
    /// Replacement text (/ActualText) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ActualText { get; internal set; }

    /// <summary>
    /// Alternate description (/Alt) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Alt { get; internal set; }

    /// <summary>
    /// Expansion of an abbreviation or acronym (/E) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ExpandedForm { get; internal set; }

    /// <summary>
    /// Language tag (/Lang) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Lang { get; internal set; }

    /// <inheritdoc />
    public override PdfScopeType ScopeType => PdfScopeType.MarkedContent;

    /// <inheritdoc />
    public override string ToString() => $"{TagName}{FormatOptionalContent()}";
}

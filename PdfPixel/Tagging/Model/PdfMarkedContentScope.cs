using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Text;
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
    /// Initializes a new tagged <see cref="PdfMarkedContentScope"/> with the tag operand, marked content identifier,
    /// property list and structure element of the marked content.
    /// </summary>
    internal PdfMarkedContentScope(
        in PdfString tagName,
        PdfMarkedContentTag tagType,
        int? mcid,
        PdfDictionary? propertiesDictionary,
        PdfStructureElement? structureElement)
        : this(tagName, tagType, optionalContent: null, associatedFiles: null, structureElement)
    {
        Mcid = mcid;

        if (propertiesDictionary == null)
        {
            return;
        }

        if (tagType == PdfMarkedContentTag.Artifact)
        {
            Artifact = new PdfArtifactProperties(propertiesDictionary);
        }
        else if (tagType == PdfMarkedContentTag.Span)
        {
            ActualText = propertiesDictionary.GetString(PdfTokens.ActualTextKey);
            AlternateDescription = propertiesDictionary.GetString(PdfTokens.AltKey);
            ExpandedForm = propertiesDictionary.GetString(PdfTokens.ExpandedFormKey);
            Lang = propertiesDictionary.GetString(PdfTokens.LangKey);
        }
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
    public int? Mcid { get; }

    /// <summary>
    /// Whether the marked content is an artifact (Artifact tag).
    /// </summary>
    public bool IsArtifact => TagType == PdfMarkedContentTag.Artifact;

    /// <summary>
    /// Property list of the artifact, or <see langword="null"/> when the content is not an artifact or
    /// the artifact has no property list.
    /// </summary>
    public PdfArtifactProperties? Artifact { get; }

    /// <summary>
    /// Replacement text (/ActualText) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ActualText { get; }

    /// <summary>
    /// Alternate description (/Alt) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? AlternateDescription { get; }

    /// <summary>
    /// Expansion of an abbreviation or acronym (/E) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ExpandedForm { get; }

    /// <summary>
    /// Language tag (/Lang) of a Span sequence, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Lang { get; }

    /// <inheritdoc />
    public override PdfScopeType ScopeType => PdfScopeType.MarkedContent;

    /// <inheritdoc />
    public override string ToString() => $"{TagName}{FormatOptionalContent()}";
}

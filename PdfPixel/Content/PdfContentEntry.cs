using PdfPixel.Files;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using System.Collections.Generic;

namespace PdfPixel.Content;

/// <summary>
/// A content item extracted from a content tree, with the /Alt, /ActualText and /AF of the item or of the content enclosing it.
/// </summary>
public readonly struct PdfContentEntry : IPdfAssociatedFilesHost
{
    /// <summary>
    /// Initializes a <see cref="PdfContentEntry"/> with the item and the /Alt, /ActualText and /AF that apply to it.
    /// </summary>
    internal PdfContentEntry(
        PdfContentItem item,
        PdfString? alternateDescription,
        PdfString? actualText,
        IReadOnlyList<PdfFileSpecification>? associatedFiles)
    {
        Item = item;
        AlternateDescription = alternateDescription;
        ActualText = actualText;
        AssociatedFiles = associatedFiles;
    }

    /// <summary>
    /// The extracted content item.
    /// </summary>
    public PdfContentItem Item { get; }

    /// <summary>
    /// Alternate description (/Alt), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? AlternateDescription { get; }

    /// <summary>
    /// Replacement text (/ActualText), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ActualText { get; }

    /// <inheritdoc />
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// Returns the matrix mapping the content of <see cref="Item"/> into the space of the content tree.
    /// </summary>
    public PdfMatrix GetMatrix() => PdfContentExtractionUtilities.GetContentMatrix(Item);

    /// <summary>
    /// Returns the area the content of <see cref="Item"/> covers, in the space of the content tree.
    /// </summary>
    public PdfRectangle GetBounds() => GetMatrix().MapRect(Item.Command.Bounds);
}

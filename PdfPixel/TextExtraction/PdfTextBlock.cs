using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// A node in the text content tree built during command execution.
/// Each block corresponds to a marked content scope (or the root) and
/// contains inline characters and child blocks.
/// </summary>
public class PdfTextBlock
{
    /// <summary>
    /// Text markup from the marked content scope that opened this block.
    /// Null for the root block.
    /// </summary>
    public PdfTextMarkup? Markup { get; }

    /// <summary>
    /// Parent block, or null for the root.
    /// </summary>
    public PdfTextBlock? Parent { get; }

    /// <summary>
    /// Characters directly contained in this block.
    /// </summary>
    public List<PdfCharacter> Characters { get; } = [];

    /// <summary>
    /// Child blocks opened by nested marked content scopes.
    /// </summary>
    public List<PdfTextBlock> Children { get; } = [];

    /// <summary>
    /// Initializes a root <see cref="PdfTextBlock"/> with no markup or parent.
    /// </summary>
    internal PdfTextBlock()
    {
    }

    /// <summary>
    /// Initializes a child <see cref="PdfTextBlock"/> with the specified markup and parent.
    /// </summary>
    internal PdfTextBlock(PdfTextMarkup? markup, PdfTextBlock parent)
    {
        Markup = markup;
        Parent = parent;
    }
}

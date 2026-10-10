using PdfPixel.Models;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// One node of the presentation tree of a <see cref="PdfUserOptionalContentConfiguration"/>, built from /Order:
/// an optional content group, or a nested list with an optional label.
/// </summary>
public sealed class PdfOptionalContentItem
{
    internal PdfOptionalContentItem(PdfString? label, PdfOptionalContentGroupState? state, IReadOnlyList<PdfOptionalContentItem> children)
    {
        Label = label;
        State = state;
        Children = children;
    }

    /// <summary>
    /// Label of a nested list, or <see langword="null"/> for a group or an unlabeled list.
    /// </summary>
    public PdfString? Label { get; }

    /// <summary>
    /// State of the group this node shows, or <see langword="null"/> for a nested list.
    /// </summary>
    public PdfOptionalContentGroupState? State { get; }

    /// <summary>
    /// Nodes of a nested list; empty for a group.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentItem> Children { get; }
}

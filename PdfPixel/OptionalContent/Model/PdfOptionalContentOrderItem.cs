using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// One element of a configuration's /Order array: an optional content group, or a nested array
/// with an optional text label.
/// </summary>
public sealed class PdfOptionalContentOrderItem
{
    /// <summary>
    /// Initializes an item referring to an optional content group.
    /// </summary>
    /// <param name="group">Reference of the group.</param>
    public PdfOptionalContentOrderItem(in PdfReference group)
    {
        Group = group;
        Children = Array.Empty<PdfOptionalContentOrderItem>();
    }

    /// <summary>
    /// Initializes an item holding a nested array.
    /// </summary>
    /// <param name="label">Text label of the nested array, or null when unlabeled.</param>
    /// <param name="children">Elements of the nested array.</param>
    public PdfOptionalContentOrderItem(PdfString? label, IReadOnlyList<PdfOptionalContentOrderItem> children)
    {
        Label = label;
        Children = children;
    }

    /// <summary>
    /// Referenced optional content group, or <see langword="null"/> for a nested array.
    /// </summary>
    public PdfReference? Group { get; }

    /// <summary>
    /// Text label of a nested array, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Label { get; }

    /// <summary>
    /// Elements of a nested array; empty for a group.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentOrderItem> Children { get; }

    /// <summary>
    /// Reads an /Order array, or returns null when absent.
    /// </summary>
    /// <param name="order">The /Order array.</param>
    internal static List<PdfOptionalContentOrderItem>? FromArray(PdfArray? order)
    {
        if (order == null)
        {
            return null;
        }

        List<PdfOptionalContentOrderItem> items = new(order.Count);
        for (int index = 0; index < order.Count; index++)
        {
            PdfArray? nested = order.GetArray(index);
            if (nested != null)
            {
                items.Add(FromNestedArray(nested));
                continue;
            }

            PdfReference? group = order.GetReference(index);
            if (group != null)
            {
                items.Add(new PdfOptionalContentOrderItem(group.Value));
            }
        }

        return items;
    }

    private static PdfOptionalContentOrderItem FromNestedArray(PdfArray nested)
    {
        PdfString? label = (nested.Count > 0 && nested.GetReference(0) == null) ? nested.GetString(0) : null;
        List<PdfOptionalContentOrderItem> children = FromArray(nested) ?? [];
        return new PdfOptionalContentOrderItem(label, children);
    }
}

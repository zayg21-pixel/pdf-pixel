using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Actions;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable document outline (bookmarks) of a panel.
/// </summary>
public sealed class PdfOutlinesViewModel
{
    private readonly PdfPanelActions _actions;

    /// <summary>
    /// Initializes a view model over the outline of the document <paramref name="pages"/> are decoded from.
    /// </summary>
    /// <param name="pages">The panel's pages.</param>
    /// <param name="actions">The panel's actions, performing activated items.</param>
    public PdfOutlinesViewModel(PdfPanelPageCollection pages, PdfPanelActions actions)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        _actions = actions ?? throw new ArgumentNullException(nameof(actions));

        IReadOnlyList<PdfOutlineItem>? outlineItems = pages.Document.Outlines.Items;
        if (outlineItems == null)
        {
            Items = Array.Empty<PdfOutlineItemViewModel>();
            return;
        }

        Items = PdfOutlineItemViewModel.CreateItems(outlineItems);
    }

    /// <summary>
    /// Raised when an item was activated and the panel performs its action or navigates to its destination.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Top-level outline items; empty when the document has no outline.
    /// </summary>
    public IReadOnlyList<PdfOutlineItemViewModel> Items { get; }

    /// <summary>
    /// Whether the document has outline items.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Performs the action of <paramref name="item"/>, or navigates to its destination when it has no action.
    /// </summary>
    /// <param name="item">The activated item.</param>
    public void Activate(PdfOutlineItemViewModel item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        _actions.ActivateOutlineItem(item.Item);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

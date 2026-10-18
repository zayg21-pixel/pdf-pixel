using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable page thumbnails of a panel.
/// </summary>
public sealed class PdfThumbnailsViewModel
{
    private readonly PdfPanelActions _actions;
    private readonly List<PdfThumbnailViewModel> _items;

    /// <summary>
    /// Initializes a view model over <paramref name="thumbnails"/> of <paramref name="pages"/>.
    /// </summary>
    /// <param name="thumbnails">The panel's thumbnails.</param>
    /// <param name="pages">The panel's pages.</param>
    /// <param name="actions">The panel's actions, navigating to activated pages.</param>
    public PdfThumbnailsViewModel(PdfPanelThumbnails thumbnails, PdfPanelPageCollection pages, PdfPanelActions actions)
    {
        if (thumbnails == null)
        {
            throw new ArgumentNullException(nameof(thumbnails));
        }

        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _items = new List<PdfThumbnailViewModel>(pages.Count);

        foreach (PdfPanelPage page in pages)
        {
            _items.Add(new PdfThumbnailViewModel(thumbnails, page.PageNumber, page.Info));
        }

        thumbnails.ThumbnailReady += OnThumbnailReady;
        thumbnails.Cleared += OnCleared;
    }

    /// <summary>
    /// Raised when a thumbnail was activated and the panel navigates to its page.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Thumbnails of the pages, one per page in page order.
    /// </summary>
    public IReadOnlyList<PdfThumbnailViewModel> Items => _items;

    /// <summary>
    /// Navigates to the page of <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The activated thumbnail.</param>
    public void Activate(PdfThumbnailViewModel item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        _actions.ScrollToPage(item.PageNumber);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnThumbnailReady(object? sender, PdfPanelThumbnailEventArgs args) => _items[args.PageNumber - 1].OnThumbnailReady(args.Png);

    private void OnCleared(object? sender, EventArgs args)
    {
        foreach (PdfThumbnailViewModel item in _items)
        {
            item.OnCleared();
        }
    }
}

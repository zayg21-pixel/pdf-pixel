using PdfPixel.Models;
using PdfPixel.PdfPanel.Actions;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable page thumbnails of a panel.
/// </summary>
public sealed class PdfThumbnailsViewModel
{
    private readonly PdfPanelActions _actions;

    /// <summary>
    /// Initializes a view model over the thumbnails of <paramref name="pages"/>.
    /// </summary>
    /// <param name="pages">The panel's pages.</param>
    /// <param name="actions">The panel's actions, navigating to activated pages.</param>
    public PdfThumbnailsViewModel(PdfPanelPageCollection pages, PdfPanelActions actions)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        _actions = actions ?? throw new ArgumentNullException(nameof(actions));

        IReadOnlyList<IPdfPage> documentPages = pages.ContentProvider.Document.Pages;
        List<PdfThumbnailViewModel> items = new(pages.Count);

        foreach (PdfPanelPage page in pages)
        {
            items.Add(new PdfThumbnailViewModel(documentPages[page.PageNumber - 1], page.PageNumber, page.Info, pages.DocumentLocker));
        }

        Items = items;
    }

    /// <summary>
    /// Raised when a thumbnail was activated and the panel navigates to its page.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Thumbnails of the pages, one per page in page order.
    /// </summary>
    public IReadOnlyList<PdfThumbnailViewModel> Items { get; }

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
}

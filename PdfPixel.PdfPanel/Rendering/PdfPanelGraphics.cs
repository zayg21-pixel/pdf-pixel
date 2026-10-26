using PdfPixel.Geometry;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// User interface graphics drawn over page content, stored per page and per <see cref="PdfPanelGraphicsLayer"/>.
/// </summary>
internal sealed class PdfPanelGraphics : IDisposable
{
    private readonly PdfPanelDocument _panelDocument;
    private readonly Dictionary<int, PdfPanelPageGraphics> _pages = [];
    private readonly List<int> _updatedPageNumbers = [];

    /// <summary>
    /// Initializes empty graphics for the pages of <paramref name="panelDocument"/>.
    /// </summary>
    internal PdfPanelGraphics(PdfPanelDocument panelDocument)
        => _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));

    /// <summary>
    /// Replaces the picture of <paramref name="layer"/> on the given page. A <see langword="null"/> picture empties the layer.
    /// </summary>
    internal void Update(int pageNumber, PdfPanelGraphicsLayer layer, SKPicture? picture)
    {
        if (!_pages.TryGetValue(pageNumber, out PdfPanelPageGraphics? page))
        {
            if (picture == null)
            {
                return;
            }

            page = new PdfPanelPageGraphics();
            _pages[pageNumber] = page;
        }

        page.Update(layer, picture);
    }

    /// <summary>
    /// Composes the picture of every page with an updated layer.
    /// </summary>
    /// <returns>Numbers of the pages whose picture changed.</returns>
    internal IReadOnlyList<int> Update()
    {
        _updatedPageNumbers.Clear();

        foreach (KeyValuePair<int, PdfPanelPageGraphics> page in _pages)
        {
            if (!page.Value.IsUpdated)
            {
                continue;
            }

            PdfSize pageSize = _panelDocument.Pages[page.Key - 1].Size;
            page.Value.Compose(SKRect.Create(pageSize.Width, pageSize.Height));
            _updatedPageNumbers.Add(page.Key);
        }

        foreach (int pageNumber in _updatedPageNumbers)
        {
            PdfPanelPageGraphics page = _pages[pageNumber];

            if (page.IsEmpty)
            {
                page.Dispose();
                _pages.Remove(pageNumber);
            }
        }

        return _updatedPageNumbers;
    }

    /// <summary>
    /// Returns the composed picture of the given page, or <see langword="null"/> if the page has no graphics.
    /// </summary>
    internal SKPicture? GetPicture(int pageNumber)
        => (_pages.TryGetValue(pageNumber, out PdfPanelPageGraphics? page)) ? page.Picture : null;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (PdfPanelPageGraphics page in _pages.Values)
        {
            page.Dispose();
        }

        _pages.Clear();
    }
}

using PdfPixel.Commands;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// User interface graphics drawn over page content, stored per page and per <see cref="PdfPanelGraphicsLayer"/>.
/// </summary>
internal sealed class PdfPanelGraphics
{
    private readonly Dictionary<int, PdfPanelPageGraphics> _pages = [];
    private readonly List<int> _updatedPageNumbers = [];

    /// <summary>
    /// Replaces the recording of <paramref name="layer"/> on the given page. A <see langword="null"/> recording empties
    /// the layer.
    /// </summary>
    internal void Update(int pageNumber, PdfPanelGraphicsLayer layer, PdfCommandRecorder? recording)
    {
        if (!_pages.TryGetValue(pageNumber, out PdfPanelPageGraphics? page))
        {
            if (recording == null)
            {
                return;
            }

            page = new PdfPanelPageGraphics();
            _pages[pageNumber] = page;
        }

        page.Update(layer, recording);
    }

    /// <summary>
    /// Composes the recording of every page with an updated layer.
    /// </summary>
    /// <returns>Numbers of the pages whose recording changed.</returns>
    internal IReadOnlyList<int> Update()
    {
        _updatedPageNumbers.Clear();

        foreach (KeyValuePair<int, PdfPanelPageGraphics> page in _pages)
        {
            if (!page.Value.IsUpdated)
            {
                continue;
            }

            page.Value.Compose();
            _updatedPageNumbers.Add(page.Key);
        }

        foreach (int pageNumber in _updatedPageNumbers)
        {
            if (_pages[pageNumber].IsEmpty)
            {
                _pages.Remove(pageNumber);
            }
        }

        return _updatedPageNumbers;
    }

    /// <summary>
    /// Returns the composed recording of the given page, or <see langword="null"/> if the page has no graphics.
    /// </summary>
    internal PdfCommandRecorder? GetRecording(int pageNumber)
        => (_pages.TryGetValue(pageNumber, out PdfPanelPageGraphics? page)) ? page.Recording : null;
}

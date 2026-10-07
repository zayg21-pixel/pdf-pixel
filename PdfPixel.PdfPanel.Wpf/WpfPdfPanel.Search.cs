using PdfPixel.PdfPanel.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Contains the text search of the panel.
/// </summary>
public partial class WpfPdfPanel
{
    private void OnSearchMatchesChanged(object? sender, EventArgs e)
    {
        if (_context == null)
        {
            return;
        }

        IReadOnlyList<PdfPanelSearchMatch> matches = _context.Search.Matches;
        ObservableCollection<PdfPanelSearchMatch> results = SearchResults;
        int resultIndex = 0;

        foreach (PdfPanelSearchMatch match in matches)
        {
            while (resultIndex < results.Count && results[resultIndex].Range.CompareTo(match.Range) < 0)
            {
                results.RemoveAt(resultIndex);
            }

            if (resultIndex < results.Count && results[resultIndex].Range.CompareTo(match.Range) == 0)
            {
                resultIndex++;
                continue;
            }

            results.Insert(resultIndex, match);
            resultIndex++;
        }

        while (results.Count > resultIndex)
        {
            results.RemoveAt(results.Count - 1);
        }

        if (CurrentSearchResult != null && !results.Contains(CurrentSearchResult.Value))
        {
            CurrentSearchResult = null;
        }
    }

    private void SelectNextSearchResult()
    {
        if (_context == null)
        {
            return;
        }

        _context.Search.CurrentMatch = CurrentSearchResult;
        _context.Search.Next();
        CurrentSearchResult = _context.Search.CurrentMatch;
    }

    private void SelectPreviousSearchResult()
    {
        if (_context == null)
        {
            return;
        }

        _context.Search.CurrentMatch = CurrentSearchResult;
        _context.Search.Previous();
        CurrentSearchResult = _context.Search.CurrentMatch;
    }

    private void ClearSearchResults()
    {
        SearchResults.Clear();
        CurrentSearchResult = null;
    }
}

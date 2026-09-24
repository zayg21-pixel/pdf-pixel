using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Searches the text of <see cref="PdfPanelTextLayer"/> and holds the matches of the current search query.
/// </summary>
public sealed class PdfPanelTextSearchEngine
{
    private readonly PdfPanelTextLayer _textLayer;
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly SortedDictionary<int, List<PdfPanelSearchMatch>> _pageMatches = [];
    private readonly List<PdfPanelSearchMatch> _matches = [];
    private readonly PdfPanelSearchOptions _options = new();
    private readonly List<int> _characterIndexes = [];
    private string? _query;

    /// <summary>
    /// Initializes the engine that searches the text of <paramref name="textLayer"/> across the pages of <paramref name="contentProvider"/>.
    /// </summary>
    public PdfPanelTextSearchEngine(PdfPanelTextLayer textLayer, IPdfPageContentProvider contentProvider)
    {
        _textLayer = textLayer ?? throw new ArgumentNullException(nameof(textLayer));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
    }

    /// <summary>
    /// Raised when <see cref="Matches"/> changes.
    /// </summary>
    public event EventHandler? MatchesChanged;

    /// <summary>
    /// Matches of the current search query on the pages whose characters have been extracted so far, ordered by page.
    /// </summary>
    public IReadOnlyList<PdfPanelSearchMatch> Matches => _matches;

    /// <summary>
    /// Searches every page with extracted characters for <paramref name="query"/> when the query or the options
    /// differ from the last search. A <see langword="null"/> or empty query clears the matches.
    /// </summary>
    /// <returns><see langword="true"/> if the matches were searched again.</returns>
    public bool Update(string? query, PdfPanelSearchOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (query == _query
            && options.MatchCase == _options.MatchCase
            && options.WholeWord == _options.WholeWord)
        {
            return false;
        }

        _query = query;
        _options.MatchCase = options.MatchCase;
        _options.WholeWord = options.WholeWord;
        _pageMatches.Clear();

        for (int pageNumber = 1; pageNumber <= _contentProvider.GetPagesCount(); pageNumber++)
        {
            SearchPageCharacters(pageNumber);
        }

        RebuildMatches();

        return true;
    }

    /// <summary>
    /// Searches a page whose characters have just been extracted for the current search query.
    /// </summary>
    /// <returns><see langword="true"/> if the page added matches.</returns>
    public bool SearchPage(int pageNumber)
    {
        if (_pageMatches.ContainsKey(pageNumber) || !SearchPageCharacters(pageNumber))
        {
            return false;
        }

        RebuildMatches();

        return true;
    }

    /// <summary>
    /// Returns the matches on the given page, or <see langword="null"/> if the page has none.
    /// </summary>
    internal IReadOnlyList<PdfPanelSearchMatch>? GetPageMatches(int pageNumber)
        => (_pageMatches.TryGetValue(pageNumber, out List<PdfPanelSearchMatch>? pageMatches)) ? pageMatches : null;

    private bool SearchPageCharacters(int pageNumber)
    {
        string? query = _query;

        if (query == null || query.Length == 0)
        {
            return false;
        }

        string? pageText = _textLayer.GetPageText(pageNumber, _characterIndexes);

        if (pageText == null)
        {
            return false;
        }

        StringComparison comparison = (_options.MatchCase) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        List<PdfPanelSearchMatch>? pageMatches = null;
        int position = pageText.IndexOf(query, comparison);

        while (position >= 0)
        {
            int end = position + query.Length;

            if (!_options.WholeWord || IsWordBoundary(pageText, position, end))
            {
                int startIndex = _characterIndexes[position];
                int lastIndex = _characterIndexes[end - 1];
                PdfPanelTextRange range = new(pageNumber, startIndex, lastIndex - startIndex + 1);
                PdfRectangle? bounds = _textLayer.GetBounds(range);

                if (bounds != null)
                {
                    if (pageMatches == null)
                    {
                        pageMatches = new List<PdfPanelSearchMatch>();
                    }

                    pageMatches.Add(new PdfPanelSearchMatch(range, bounds.Value));
                }
            }

            position = pageText.IndexOf(query, end, comparison);
        }

        if (pageMatches == null)
        {
            return false;
        }

        _pageMatches[pageNumber] = pageMatches;

        return true;
    }

    private void RebuildMatches()
    {
        _matches.Clear();

        foreach (List<PdfPanelSearchMatch> pageMatches in _pageMatches.Values)
        {
            _matches.AddRange(pageMatches);
        }

        MatchesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsWordBoundary(string text, int start, int end)
    {
        return (start == 0 || !char.IsLetterOrDigit(text[start - 1]))
            && (end == text.Length || !char.IsLetterOrDigit(text[end]));
    }
}

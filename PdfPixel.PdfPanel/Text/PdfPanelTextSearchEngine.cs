using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Settings;
using PdfPixel.TextExtraction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Searches the text of <see cref="PdfPanelTextLayer"/> and holds the matches of the current search query.
/// </summary>
public sealed class PdfPanelTextSearchEngine
{
    private readonly PdfPanelTextLayer _textLayer;
    private readonly PdfPageContentProvider _contentProvider;
    private readonly SortedDictionary<int, List<PdfPanelSearchMatch>> _pageMatches = [];
    private readonly List<PdfPanelSearchMatch> _matches = [];
    private readonly List<int> _characterIndexes = [];
    private readonly HashSet<PdfPanelTextRange> _matchedRanges = [];
    private readonly StringBuilder _textBuilder = new();
    private readonly StringBuilder _normalizationBuilder = new();
    private PdfPanelSearchSettings _searchSettings = new();
    private string? _query;
    private string? _normalizedQuery;

    /// <summary>
    /// Initializes the engine that searches the text of <paramref name="textLayer"/> across the pages of <paramref name="contentProvider"/>.
    /// </summary>
    internal PdfPanelTextSearchEngine(PdfPanelTextLayer textLayer, PdfPageContentProvider contentProvider)
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
    /// The match highlighted as current, or <see langword="null"/> when there is none.
    /// </summary>
    public PdfPanelSearchMatch? CurrentMatch { get; private set; }

    /// <summary>
    /// Searches every page with extracted characters for <paramref name="query"/> when the query or <paramref name="searchSettings"/>
    /// differ from the last search. A <see langword="null"/> or empty query clears the matches.
    /// </summary>
    /// <returns><see langword="true"/> if the matches were searched again.</returns>
    public bool Update(string? query, PdfPanelSearchSettings searchSettings)
    {
        if (searchSettings == null)
        {
            throw new ArgumentNullException(nameof(searchSettings));
        }

        if (query == _query && searchSettings.Equals(_searchSettings))
        {
            return false;
        }

        _query = query;
        _searchSettings = searchSettings.Clone();
        _normalizedQuery = (query == null) ? null : NormalizeQuery(query);
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
    /// Sets the match highlighted as current.
    /// </summary>
    /// <returns><see langword="true"/> if the current match changed.</returns>
    internal bool UpdateCurrentMatch(PdfPanelSearchMatch? currentMatch)
    {
        if (currentMatch?.Range == CurrentMatch?.Range)
        {
            return false;
        }

        CurrentMatch = currentMatch;

        return true;
    }

    /// <summary>
    /// Returns the matches on the given page, or <see langword="null"/> if the page has none.
    /// </summary>
    internal IReadOnlyList<PdfPanelSearchMatch>? GetPageMatches(int pageNumber)
        => (_pageMatches.TryGetValue(pageNumber, out List<PdfPanelSearchMatch>? pageMatches)) ? pageMatches : null;

    private bool SearchPageCharacters(int pageNumber)
    {
        string? query = _normalizedQuery;

        if (query == null || query.Length == 0)
        {
            return false;
        }

        PdfWord[]? words = _textLayer.GetWords(pageNumber);

        if (words == null)
        {
            return false;
        }

        List<PdfPanelSearchMatch> pageMatches = [];
        _matchedRanges.Clear();

        FindMatches(pageNumber, GetNormalizedPageText(words, keepBoundaryHyphens: false), query, pageMatches);

        if (HasHyphenatedWord(words))
        {
            FindMatches(pageNumber, GetNormalizedPageText(words, keepBoundaryHyphens: true), query, pageMatches);
            pageMatches.Sort(CompareMatches);
        }

        if (pageMatches.Count == 0)
        {
            return false;
        }

        _pageMatches[pageNumber] = pageMatches;

        return true;
    }

    /// <summary>
    /// Adds the matches of <paramref name="query"/> in <paramref name="pageText"/> whose range is not in <paramref name="pageMatches"/> yet.
    /// </summary>
    private void FindMatches(int pageNumber, string pageText, string query, List<PdfPanelSearchMatch> pageMatches)
    {
        StringComparison comparison = (_searchSettings.MatchCase) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int position = pageText.IndexOf(query, comparison);

        while (position >= 0)
        {
            int end = position + query.Length;

            if (!_searchSettings.WholeWord || IsWordBoundary(pageText, position, end))
            {
                int startIndex = _characterIndexes[position];
                int lastIndex = _characterIndexes[end - 1];
                PdfPanelTextRange range = new(pageNumber, startIndex, lastIndex - startIndex + 1);
                PdfRectangle? bounds = _textLayer.GetBounds(range);

                if (bounds != null && _matchedRanges.Add(range))
                {
                    pageMatches.Add(new PdfPanelSearchMatch(range, bounds.Value));
                }
            }

            position = pageText.IndexOf(query, end, comparison);
        }
    }

    /// <summary>
    /// Returns the normalized text of every character of <paramref name="words"/>, with the parts of a hyphenated word joined
    /// without their line break, and without their hyphen unless <paramref name="keepBoundaryHyphens"/> is set.
    /// <see cref="_characterIndexes"/> is filled with the index of the character each text position belongs to.
    /// </summary>
    private string GetNormalizedPageText(PdfWord[] words, bool keepBoundaryHyphens)
    {
        _textBuilder.Clear();
        _characterIndexes.Clear();

        foreach (PdfWord word in words)
        {
            for (int partIndex = 0; partIndex < word.Parts.Length; partIndex++)
            {
                PdfWordPart part = word.Parts[partIndex];
                int characterCount = part.Characters.Length;

                if (partIndex < word.Parts.Length - 1)
                {
                    characterCount -= keepBoundaryHyphens ? 1 : 2;
                }

                for (int characterIndex = 0; characterIndex < characterCount; characterIndex++)
                {
                    string? characterText = part.Characters[characterIndex].Text;

                    if (characterText == null)
                    {
                        continue;
                    }

                    AppendSearchText(characterText, part.StartIndex + characterIndex);
                }
            }
        }

        return _textBuilder.ToString();
    }

    /// <summary>
    /// Returns <paramref name="query"/> normalized the same way as the page text.
    /// </summary>
    private string NormalizeQuery(string query)
    {
        _textBuilder.Clear();
        _characterIndexes.Clear();

        AppendSearchText(query, 0);

        return _textBuilder.ToString();
    }

    /// <summary>
    /// Appends the normalized <paramref name="text"/> to <see cref="_textBuilder"/>, with every whitespace run as a single space,
    /// and maps each appended position to <paramref name="characterIndex"/>.
    /// </summary>
    private void AppendSearchText(string text, int characterIndex)
    {
        foreach (char character in NormalizeText(text))
        {
            if (char.IsWhiteSpace(character))
            {
                if (_textBuilder.Length > 0 && _textBuilder[_textBuilder.Length - 1] == ' ')
                {
                    continue;
                }

                _textBuilder.Append(' ');
            }
            else
            {
                _textBuilder.Append(character);
            }

            _characterIndexes.Add(characterIndex);
        }
    }

    /// <summary>
    /// Returns <paramref name="text"/> in compatibility decomposition (FormKD), without nonspacing marks
    /// unless <see cref="PdfPanelSearchSettings.MatchDiacritics"/> is set.
    /// </summary>
    private string NormalizeText(string text)
    {
        if (IsAscii(text))
        {
            return text;
        }

        string decomposedText = text.Normalize(NormalizationForm.FormKD);

        if (_searchSettings.MatchDiacritics)
        {
            return decomposedText;
        }

        _normalizationBuilder.Clear();

        foreach (char character in decomposedText)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                _normalizationBuilder.Append(character);
            }
        }

        return _normalizationBuilder.ToString();
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

    private static bool HasHyphenatedWord(PdfWord[] words)
    {
        foreach (PdfWord word in words)
        {
            if (word.Parts.Length > 1)
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareMatches(PdfPanelSearchMatch left, PdfPanelSearchMatch right) => left.Range.CompareTo(right.Range);

    private static bool IsAscii(string text)
    {
        foreach (char character in text)
        {
            if (character > 0x7F)
            {
                return false;
            }
        }

        return true;
    }
}

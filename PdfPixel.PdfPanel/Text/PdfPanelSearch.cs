using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Skia;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Search of the panel's text, holding the matches of the current search query.
/// </summary>
public sealed class PdfPanelSearch
{
    private readonly PdfPanelContext _context;
    private readonly PdfPanelText _text;
    private readonly PdfPageContentProvider _contentProvider;
    private readonly PdfPanelGraphics _graphics;
    private readonly SortedDictionary<int, List<PdfPanelSearchMatch>> _pageMatches = [];
    private readonly List<PdfPanelSearchMatch> _matches = [];
    private readonly List<int> _characterIndexes = [];
    private readonly HashSet<PdfPanelTextRange> _matchedRanges = [];
    private readonly StringBuilder _textBuilder = new();
    private readonly StringBuilder _normalizationBuilder = new();
    private string? _query;
    private bool _matchCase;
    private bool _wholeWord;
    private bool _matchDiacritics;
    private string? _normalizedQuery;
    private PdfPanelSearchMatch? _drawnCurrentMatch;
    private PdfColor _drawnMatchColor;
    private PdfColor _drawnCurrentMatchColor;
    private float _drawnLineMergeThreshold;

    /// <summary>
    /// Initializes the search of the text of <paramref name="text"/> across the pages of <paramref name="contentProvider"/>.
    /// </summary>
    internal PdfPanelSearch(PdfPanelContext context, PdfPanelText text, PdfPageContentProvider contentProvider, PdfPanelGraphics graphics)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
    }

    /// <summary>
    /// Raised when <see cref="Matches"/> changes.
    /// </summary>
    public event EventHandler? MatchesChanged;

    /// <summary>
    /// Text to search for in the document, or <see langword="null"/> when no search is active.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Whether letter case must match.
    /// </summary>
    public bool MatchCase { get; set; }

    /// <summary>
    /// Whether a match must start and end on word boundaries.
    /// </summary>
    public bool WholeWord { get; set; }

    /// <summary>
    /// Whether diacritics must match, so that "e" does not match "é".
    /// </summary>
    public bool MatchDiacritics { get; set; }

    /// <summary>
    /// Color the search matches are highlighted with.
    /// </summary>
    public PdfColor MatchColor { get; set; } = new(255f / 255f, 200f / 255f, 0f / 255f, 100f / 255f);

    /// <summary>
    /// Color the current search match is highlighted with.
    /// </summary>
    public PdfColor CurrentMatchColor { get; set; } = new(255f / 255f, 120f / 255f, 0f / 255f, 140f / 255f);

    /// <summary>
    /// The match highlighted as current, or <see langword="null"/> when there is none.
    /// </summary>
    public PdfPanelSearchMatch? CurrentMatch { get; set; }

    /// <summary>
    /// Matches of the current search query on the pages whose characters have been extracted so far, ordered by page.
    /// </summary>
    public IReadOnlyList<PdfPanelSearchMatch> Matches => _matches;

    /// <summary>
    /// Sets <see cref="CurrentMatch"/> to the match after it, wrapping to the first match,
    /// or to the first match on or after the current page when <see cref="CurrentMatch"/> is not one of <see cref="Matches"/>.
    /// </summary>
    public void Next() => SelectMatch(1);

    /// <summary>
    /// Sets <see cref="CurrentMatch"/> to the match before it, wrapping to the last match,
    /// or to the first match on or after the current page when <see cref="CurrentMatch"/> is not one of <see cref="Matches"/>.
    /// </summary>
    public void Previous() => SelectMatch(-1);

    /// <summary>
    /// Searches every page with extracted characters for <see cref="Query"/> when the query or the match options
    /// differ from the last search, and updates the match graphics that changed.
    /// </summary>
    internal void Synchronize()
    {
        bool queryChanged = Query != _query
            || MatchCase != _matchCase
            || WholeWord != _wholeWord
            || MatchDiacritics != _matchDiacritics;

        bool appearanceChanged = !MatchColor.Equals(_drawnMatchColor)
            || !CurrentMatchColor.Equals(_drawnCurrentMatchColor)
            || _text.LineMergeThreshold != _drawnLineMergeThreshold;

        if (queryChanged)
        {
            Search();
        }

        if (queryChanged || appearanceChanged)
        {
            _drawnCurrentMatch = CurrentMatch;
            _drawnMatchColor = MatchColor;
            _drawnCurrentMatchColor = CurrentMatchColor;
            _drawnLineMergeThreshold = _text.LineMergeThreshold;

            foreach (int pageNumber in _pageMatches.Keys)
            {
                UpdateGraphics(pageNumber);
            }

            return;
        }

        if (CurrentMatch?.Range == _drawnCurrentMatch?.Range)
        {
            return;
        }

        int? previousPageNumber = _drawnCurrentMatch?.Range.PageNumber;
        int? currentPageNumber = CurrentMatch?.Range.PageNumber;

        _drawnCurrentMatch = CurrentMatch;

        if (previousPageNumber != null)
        {
            UpdateGraphics(previousPageNumber.Value);
        }

        if (currentPageNumber != null && currentPageNumber != previousPageNumber)
        {
            UpdateGraphics(currentPageNumber.Value);
        }
    }

    /// <summary>
    /// Searches a page whose characters have just been extracted for the current search query.
    /// </summary>
    internal void SearchPage(int pageNumber)
    {
        if (_pageMatches.ContainsKey(pageNumber))
        {
            return;
        }

        if (!SearchPageCharacters(pageNumber))
        {
            return;
        }

        RebuildMatches();
        UpdateGraphics(pageNumber);
    }

    private void SelectMatch(int step)
    {
        if (_matches.Count == 0)
        {
            return;
        }

        int currentIndex = _matches.FindIndex(IsCurrentMatch);

        if (currentIndex >= 0)
        {
            CurrentMatch = _matches[(currentIndex + step + _matches.Count) % _matches.Count];
            return;
        }

        int currentPageNumber = _context.GetCurrentPage();
        int pageIndex = _matches.FindIndex(match => match.Range.PageNumber >= currentPageNumber);

        CurrentMatch = _matches[Math.Max(pageIndex, 0)];
    }

    private bool IsCurrentMatch(PdfPanelSearchMatch match) => match.Range == CurrentMatch?.Range;

    private void Search()
    {
        foreach (int pageNumber in _pageMatches.Keys)
        {
            _graphics.Update(pageNumber, PdfPanelGraphicsLayer.SearchMatches, null);
        }

        _query = Query;
        _matchCase = MatchCase;
        _wholeWord = WholeWord;
        _matchDiacritics = MatchDiacritics;
        _normalizedQuery = (Query == null) ? null : NormalizeQuery(Query);
        _pageMatches.Clear();

        int pageCount = _contentProvider.GetPagesCount();

        for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            SearchPageCharacters(pageNumber);
        }

        RebuildMatches();
    }

    private void UpdateGraphics(int pageNumber)
        => _graphics.Update(pageNumber, PdfPanelGraphicsLayer.SearchMatches, CreateMatchesPicture(pageNumber));

    private SKPicture? CreateMatchesPicture(int pageNumber)
    {
        if (!_pageMatches.TryGetValue(pageNumber, out List<PdfPanelSearchMatch>? pageMatches))
        {
            return null;
        }

        PdfWord[]? words = _text.GetWords(pageNumber);

        if (words == null)
        {
            return null;
        }

        PdfPanelPageInfo pageInfo = _contentProvider.GetPageInfo(pageNumber);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.CropBox.Width, pageInfo.CropBox.Height));

        using SKPaint matchPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = _drawnMatchColor.ToSkiaColor()
        };

        using SKPaint currentMatchPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = _drawnCurrentMatchColor.ToSkiaColor()
        };

        foreach (PdfPanelSearchMatch match in pageMatches)
        {
            SKPaint paint = (match.Range == _drawnCurrentMatch?.Range) ? currentMatchPaint : matchPaint;
            PdfPanelHighlightBuilder.DrawHighlight(canvas, words, match.Range, paint, _drawnLineMergeThreshold);
        }

        return recorder.EndRecording();
    }

    private bool SearchPageCharacters(int pageNumber)
    {
        string? query = _normalizedQuery;

        if (query == null || query.Length == 0)
        {
            return false;
        }

        PdfWord[]? words = _contentProvider.GetWords(pageNumber);

        if (words == null)
        {
            return false;
        }

        if (words.Length == 0)
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
        StringComparison comparison = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int position = pageText.IndexOf(query, comparison);

        while (position >= 0)
        {
            int end = position + query.Length;

            if (!_wholeWord || IsWordBoundary(pageText, position, end))
            {
                int startIndex = _characterIndexes[position];
                int lastIndex = _characterIndexes[end - 1];
                PdfPanelTextRange range = new(pageNumber, startIndex, lastIndex - startIndex + 1);
                PdfRectangle? bounds = _text.GetBounds(range);

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
    /// unless <see cref="MatchDiacritics"/> is set.
    /// </summary>
    private string NormalizeText(string text)
    {
        if (IsAscii(text))
        {
            return text;
        }

        string decomposedText = text.Normalize(NormalizationForm.FormKD);

        if (_matchDiacritics)
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

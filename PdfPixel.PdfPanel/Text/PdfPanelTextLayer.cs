using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Settings;
using PdfPixel.Skia;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Text layer of the panel: tracks text selection and produces the highlight graphics drawn over page content.
/// </summary>
public sealed class PdfPanelTextLayer : IDisposable
{
    private readonly PdfPageContentProvider _contentProvider;
    private readonly PdfPanelSettings _settings;
    private readonly PdfPanelInputProcessor _processor;
    private readonly Dictionary<int, SKPicture> _textLayerPictures = [];
    private readonly StringBuilder _textBuilder = new();
    private int? _anchorPageNumber;
    private int? _anchorCharIndex;
    private int? _currentCharIndex;
    private PdfPanelTextRange? _selection;
    private bool _isPointerOverText;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelTextLayer"/> with the given content provider and settings,
    /// and subscribes it to the given processor.
    /// </summary>
    internal PdfPanelTextLayer(
        PdfPageContentProvider contentProvider,
        PdfPanelSettings settings,
        PdfPanelInputProcessor processor)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));

        _processor.PointerMoved += OnPointerMoved;
        _processor.PointerClicked += OnPointerClicked;
        _processor.PointerExited += OnPointerExited;
        _processor.DragStarted += OnDragStarted;
        _processor.DragMoved += OnDragMoved;
        _processor.DragEnded += OnDragEnded;
    }

    /// <summary>
    /// Raised when the words of a page have been extracted.
    /// </summary>
    public event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

    /// <summary>
    /// Whether the pointer is currently over a text character.
    /// </summary>
    public bool IsPointerOverText => _isPointerOverText;

    /// <summary>
    /// The text content of the current selection, or empty if nothing is selected.
    /// </summary>
    public string SelectedText => (_selection == null) ? string.Empty : GetText(_selection.Value);

    /// <summary>
    /// Returns the text of the characters in <paramref name="range"/>, or empty if the page's characters have not been extracted yet.
    /// </summary>
    public string GetText(in PdfPanelTextRange range)
    {
        PdfWord[]? words = GetWords(range.PageNumber);

        if (words == null)
        {
            return string.Empty;
        }

        _textBuilder.Clear();

        foreach (PdfCharacter character in EnumerateCharacters(words, range))
        {
            if (character.Text != null)
            {
                _textBuilder.Append(character.Text);
            }
        }

        return _textBuilder.ToString();
    }

    /// <summary>
    /// Returns the words of the given page in reading order, or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    public PdfWord[]? GetWords(int pageNumber)
    {
        PdfWord[]? words = _contentProvider.GetWords(pageNumber);

        if (words == null || words.Length == 0)
        {
            return null;
        }

        return words;
    }

    /// <summary>
    /// Returns the area the characters in <paramref name="range"/> cover, in unscaled page space,
    /// or <see langword="null"/> if the page's characters have not been extracted yet.
    /// </summary>
    internal PdfRectangle? GetBounds(in PdfPanelTextRange range)
    {
        PdfWord[]? words = GetWords(range.PageNumber);

        if (words == null)
        {
            return null;
        }

        PdfRectangle? bounds = null;

        foreach (PdfCharacter character in EnumerateCharacters(words, range))
        {
            bounds = (bounds == null)
                ? character.BoundingBox
                : PdfRectangle.Union(bounds.Value, character.BoundingBox);
        }

        return bounds;
    }

    /// <summary>
    /// Returns the text layer picture for the given visible page, creating it on first call from <paramref name="searchMatches"/>,
    /// <paramref name="currentMatch"/> and the selection with <paramref name="appearance"/> and <paramref name="text"/>, or <see langword="null"/> if that page has nothing to highlight.
    /// </summary>
    internal SKPicture? GetTextLayerPicture(
        int pageNumber,
        IReadOnlyList<PdfPanelSearchMatch>? searchMatches,
        PdfPanelSearchMatch? currentMatch,
        PdfPanelAppearanceSettings appearance,
        PdfPanelTextSettings text)
    {
        if (_textLayerPictures.TryGetValue(pageNumber, out SKPicture? picture))
        {
            return picture;
        }

        SKPicture? newPicture = GenerateTextLayerPicture(pageNumber, searchMatches, currentMatch, appearance, text);

        if (newPicture != null)
        {
            _textLayerPictures[pageNumber] = newPicture;
        }

        return newPicture;
    }

    /// <summary>
    /// Raises <see cref="PageTextExtracted"/> for the given page.
    /// </summary>
    internal void OnPageTextExtracted(int pageNumber) => PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber));

    /// <summary>
    /// Releases the text layer pictures of every page that is not in <paramref name="visiblePages"/>.
    /// </summary>
    internal void EvictExcept(IReadOnlyList<VisiblePageInfo> visiblePages)
    {
        foreach (int pageNumber in _textLayerPictures.Keys.Where(key => !visiblePages.Any(page => page.PageNumber == key)).ToList())
        {
            Invalidate(pageNumber);
        }
    }

    /// <summary>
    /// Releases the text layer picture of the given page so it is created again on its next draw.
    /// </summary>
    internal void Invalidate(int pageNumber)
    {
        if (_textLayerPictures.TryGetValue(pageNumber, out SKPicture? picture))
        {
            picture.Dispose();
            _textLayerPictures.Remove(pageNumber);
        }
    }

    /// <summary>
    /// Releases the text layer pictures of every page so they are created again on their next draw.
    /// </summary>
    internal void Clear()
    {
        foreach (SKPicture picture in _textLayerPictures.Values)
        {
            picture.Dispose();
        }

        _textLayerPictures.Clear();
    }

    private void OnPointerMoved(object? sender, PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled)
        {
            _isPointerOverText = false;
            return;
        }

        _isPointerOverText = HitTestCharacter(args.Position, _settings.Interaction.CharacterHitRadius) != null;

        if (_isPointerOverText)
        {
            args.Cursor = PdfPanelCursor.IBeam;
        }
    }

    private void OnPointerClicked(object? sender, PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled)
        {
            return;
        }

        ClearSelection();
    }

    private void OnPointerExited(object? sender, EventArgs args) => _isPointerOverText = false;

    private void OnDragStarted(object? sender, PdfPanelDragEventArgs args)
    {
        ClearSelection();

        PdfPanelPagePoint? anchorPoint = args.StartPosition.PagePoint;

        if (anchorPoint == null)
        {
            return;
        }

        int? charIndex = HitTestCharacter(args.StartPosition, _settings.Interaction.CharacterHitRadius);

        if (charIndex == null)
        {
            return;
        }

        _anchorPageNumber = anchorPoint.Value.PageNumber;
        _anchorCharIndex = charIndex.Value;

        ExtendSelection(args.Position);
    }

    private void OnDragMoved(object? sender, PdfPanelDragEventArgs args) => ExtendSelection(args.Position);

    private void OnDragEnded(object? sender, PdfPanelDragEventArgs args)
    {
        ExtendSelection(args.Position);

        if (_selection == null)
        {
            _anchorPageNumber = null;
        }

        _anchorCharIndex = null;
        _currentCharIndex = null;
    }

    private void ExtendSelection(in PdfPanelPointerPosition position)
    {
        PdfPanelPagePoint? pagePoint = position.PagePoint;

        if (_anchorPageNumber == null || _anchorCharIndex == null || pagePoint == null)
        {
            return;
        }

        if (pagePoint.Value.PageNumber != _anchorPageNumber.Value)
        {
            return;
        }

        PdfWord[]? words = GetWords(pagePoint.Value.PageNumber);

        if (words == null)
        {
            return;
        }

        int? charIndex = HitTestCharacterNearest(words, pagePoint.Value.Position);

        if (charIndex == null || charIndex == _currentCharIndex)
        {
            return;
        }

        _currentCharIndex = charIndex;

        PdfWordPart[] lastParts = words[words.Length - 1].Parts;
        PdfWordPart lastPart = lastParts[lastParts.Length - 1];
        int start = Math.Max(Math.Min(_anchorCharIndex.Value, charIndex.Value), 0);
        int end = Math.Min(Math.Max(_anchorCharIndex.Value, charIndex.Value), lastPart.StartIndex + lastPart.Characters.Length - 1);
        _selection = new PdfPanelTextRange(_anchorPageNumber.Value, start, end - start + 1);

        Invalidate(_anchorPageNumber.Value);
    }

    private void ClearSelection()
    {
        if (_selection != null)
        {
            Invalidate(_selection.Value.PageNumber);
        }

        _anchorPageNumber = null;
        _anchorCharIndex = null;
        _currentCharIndex = null;
        _selection = null;
    }

    private int? HitTestCharacter(in PdfPanelPointerPosition position, float? maxDistance)
    {
        PdfPanelPagePoint? pagePoint = position.PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        PdfWord[]? words = GetWords(pagePoint.Value.PageNumber);

        if (words == null)
        {
            return null;
        }

        return HitTestCharacterNearest(words, pagePoint.Value.Position, maxDistance);
    }

    private SKPicture? GenerateTextLayerPicture(
        int pageNumber,
        IReadOnlyList<PdfPanelSearchMatch>? searchMatches,
        PdfPanelSearchMatch? currentMatch,
        PdfPanelAppearanceSettings appearance,
        PdfPanelTextSettings text)
    {
        PdfPanelTextRange? pageSelection = (_selection?.PageNumber == pageNumber) ? _selection : null;

        if (pageSelection == null && (searchMatches == null || searchMatches.Count == 0))
        {
            return null;
        }

        PdfWord[]? words = GetWords(pageNumber);

        if (words == null)
        {
            return null;
        }

        PdfPanelPageInfo pageInfo = _contentProvider.GetPageInfo(pageNumber);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.CropBox.Width, pageInfo.CropBox.Height));

        if (searchMatches != null)
        {
            using SKPaint searchMatchPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = appearance.SearchMatchColor.ToSkiaColor()
            };

            using SKPaint currentSearchMatchPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = appearance.CurrentSearchMatchColor.ToSkiaColor()
            };

            foreach (PdfPanelSearchMatch searchMatch in searchMatches)
            {
                SKPaint paint = (searchMatch.Range == currentMatch?.Range) ? currentSearchMatchPaint : searchMatchPaint;
                DrawHighlightStrips(canvas, words, searchMatch.Range, paint, text.LineMergeThreshold);
            }
        }

        if (pageSelection != null)
        {
            using SKPaint selectionPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = appearance.SelectionColor.ToSkiaColor()
            };

            DrawHighlightStrips(canvas, words, pageSelection.Value, selectionPaint, text.LineMergeThreshold);
        }

        return recorder.EndRecording();
    }

    private static void DrawHighlightStrips(SKCanvas canvas, PdfWord[] words, in PdfPanelTextRange range, SKPaint paint, float lineMergeThreshold)
    {
        PdfRectangle? currentStrip = null;

        foreach (PdfCharacter character in EnumerateCharacters(words, range))
        {
            PdfRectangle box = character.BoundingBox;

            if (currentStrip == null)
            {
                currentStrip = box;
            }
            else if (Math.Abs(box.Top - currentStrip.Value.Top) < currentStrip.Value.Height * lineMergeThreshold)
            {
                currentStrip = PdfRectangle.Union(currentStrip.Value, box);
            }
            else
            {
                canvas.DrawRect(currentStrip.Value.ToSkRect(), paint);
                currentStrip = box;
            }
        }

        if (currentStrip != null)
        {
            canvas.DrawRect(currentStrip.Value.ToSkRect(), paint);
        }
    }

    private static IEnumerable<PdfCharacter> EnumerateCharacters(PdfWord[] words, PdfPanelTextRange range)
    {
        int end = range.StartIndex + range.Length;

        foreach (PdfWord word in words)
        {
            foreach (PdfWordPart part in word.Parts)
            {
                if (part.StartIndex >= end)
                {
                    yield break;
                }

                int first = Math.Max(range.StartIndex - part.StartIndex, 0);
                int last = Math.Min(end - part.StartIndex, part.Characters.Length);

                for (int characterIndex = first; characterIndex < last; characterIndex++)
                {
                    yield return part.Characters[characterIndex];
                }
            }
        }
    }

    private static int? HitTestCharacterNearest(PdfWord[] words, in PdfPoint point, float? maxDistance = null)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        foreach (PdfWord word in words)
        {
            foreach (PdfWordPart part in word.Parts)
            {
                for (int characterIndex = 0; characterIndex < part.Characters.Length; characterIndex++)
                {
                    PdfRectangle characterBox = part.Characters[characterIndex].BoundingBox;
                    float dx = point.X - characterBox.MidX;
                    float dy = point.Y - characterBox.MidY;
                    float distance = (dx * dx) + (dy * dy);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = part.StartIndex + characterIndex;
                    }
                }
            }
        }

        if (maxDistance != null && closestDistance > maxDistance.Value * maxDistance.Value)
        {
            return null;
        }

        return closestIndex;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _processor.PointerMoved -= OnPointerMoved;
        _processor.PointerClicked -= OnPointerClicked;
        _processor.PointerExited -= OnPointerExited;
        _processor.DragStarted -= OnDragStarted;
        _processor.DragMoved -= OnDragMoved;
        _processor.DragEnded -= OnDragEnded;

        Clear();
    }
}

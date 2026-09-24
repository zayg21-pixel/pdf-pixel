using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Requests;
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
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly PdfPanelTextLayerParameters _parameters;
    private readonly PdfPanelInputProcessor _processor;
    private readonly Dictionary<int, SKPicture> _textLayerPictures = [];
    private readonly StringBuilder _textBuilder = new();
    private int? _anchorPageNumber;
    private int? _anchorCharIndex;
    private int? _currentCharIndex;
    private PdfPanelTextRange? _selection;
    private bool _isPointerOverText;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelTextLayer"/> with the given content provider and parameters,
    /// and subscribes it to the given processor.
    /// </summary>
    public PdfPanelTextLayer(
        IPdfPageContentProvider contentProvider,
        PdfPanelTextLayerParameters parameters,
        PdfPanelInputProcessor processor)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));

        _processor.PointerMoved += OnPointerMoved;
        _processor.PointerClicked += OnPointerClicked;
        _processor.PointerExited += OnPointerExited;
        _processor.DragStarted += OnDragStarted;
        _processor.DragMoved += OnDragMoved;
        _processor.DragEnded += OnDragEnded;
    }

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
        PdfCharacter[]? characters = GetCharacters(range.PageNumber);

        if (characters == null)
        {
            return string.Empty;
        }

        _textBuilder.Clear();

        foreach (PdfCharacter character in characters.AsSpan(range.StartIndex, range.Length))
        {
            if (character.Text != null)
            {
                _textBuilder.Append(character.Text);
            }
        }

        return _textBuilder.ToString();
    }

    /// <summary>
    /// Returns the text of every character of the given page, or <see langword="null"/> if its characters have not been
    /// extracted yet. <paramref name="characterIndexes"/> is filled with the index of the character each text position belongs to.
    /// </summary>
    internal string? GetPageText(int pageNumber, List<int> characterIndexes)
    {
        PdfCharacter[]? characters = GetCharacters(pageNumber);

        if (characters == null)
        {
            return null;
        }

        _textBuilder.Clear();
        characterIndexes.Clear();

        for (int characterIndex = 0; characterIndex < characters.Length; characterIndex++)
        {
            string? characterText = characters[characterIndex].Text;

            if (characterText == null)
            {
                continue;
            }

            _textBuilder.Append(characterText);

            for (int textIndex = 0; textIndex < characterText.Length; textIndex++)
            {
                characterIndexes.Add(characterIndex);
            }
        }

        return _textBuilder.ToString();
    }

    /// <summary>
    /// Returns the area the characters in <paramref name="range"/> cover, in unscaled page space,
    /// or <see langword="null"/> if the page's characters have not been extracted yet.
    /// </summary>
    internal PdfRectangle? GetBounds(in PdfPanelTextRange range)
    {
        PdfCharacter[]? characters = GetCharacters(range.PageNumber);

        if (characters == null)
        {
            return null;
        }

        PdfRectangle bounds = characters[range.StartIndex].BoundingBox;

        for (int characterIndex = range.StartIndex + 1; characterIndex < range.StartIndex + range.Length; characterIndex++)
        {
            bounds = PdfRectangle.Union(bounds, characters[characterIndex].BoundingBox);
        }

        return bounds;
    }

    /// <summary>
    /// Returns the text layer picture for the given visible page, creating it on first call from <paramref name="searchMatches"/>
    /// and the selection, or <see langword="null"/> if that page has nothing to highlight.
    /// </summary>
    internal SKPicture? GetTextLayerPicture(int pageNumber, IReadOnlyList<PdfPanelSearchMatch>? searchMatches)
    {
        if (_textLayerPictures.TryGetValue(pageNumber, out SKPicture? picture))
        {
            return picture;
        }

        SKPicture? newPicture = GenerateTextLayerPicture(pageNumber, searchMatches);

        if (newPicture != null)
        {
            _textLayerPictures[pageNumber] = newPicture;
        }

        return newPicture;
    }

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

    private void OnPointerMoved(object? sender, PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled)
        {
            _isPointerOverText = false;
            return;
        }

        _isPointerOverText = HitTestCharacter(args.Position, _parameters.CharacterHitRadius) != null;

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

        int? charIndex = HitTestCharacter(args.StartPosition, _parameters.CharacterHitRadius);

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

        PdfCharacter[]? characters = GetCharacters(pagePoint.Value.PageNumber);

        if (characters == null)
        {
            return;
        }

        int? charIndex = HitTestCharacterNearest(characters, pagePoint.Value.Position);

        if (charIndex == null || charIndex == _currentCharIndex)
        {
            return;
        }

        _currentCharIndex = charIndex;

        int start = Math.Max(Math.Min(_anchorCharIndex.Value, charIndex.Value), 0);
        int end = Math.Min(Math.Max(_anchorCharIndex.Value, charIndex.Value), characters.Length - 1);
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

    private PdfCharacter[]? GetCharacters(int pageNumber)
    {
        PdfCharacter[]? characters = _contentProvider.GetCharacters(pageNumber);

        if (characters == null || characters.Length == 0)
        {
            return null;
        }

        return characters;
    }

    private int? HitTestCharacter(in PdfPanelPointerPosition position, float? maxDistance)
    {
        PdfPanelPagePoint? pagePoint = position.PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        PdfCharacter[]? characters = GetCharacters(pagePoint.Value.PageNumber);

        if (characters == null)
        {
            return null;
        }

        return HitTestCharacterNearest(characters, pagePoint.Value.Position, maxDistance);
    }

    private SKPicture? GenerateTextLayerPicture(int pageNumber, IReadOnlyList<PdfPanelSearchMatch>? searchMatches)
    {
        PdfPanelTextRange? pageSelection = (_selection?.PageNumber == pageNumber) ? _selection : null;

        if (pageSelection == null && (searchMatches == null || searchMatches.Count == 0))
        {
            return null;
        }

        PdfCharacter[]? characters = GetCharacters(pageNumber);

        if (characters == null)
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
                Color = _parameters.SearchMatchColor.ToSkiaColor()
            };

            foreach (PdfPanelSearchMatch searchMatch in searchMatches)
            {
                DrawHighlightStrips(canvas, characters, searchMatch.Range, searchMatchPaint);
            }
        }

        if (pageSelection != null)
        {
            using SKPaint selectionPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = _parameters.SelectionColor.ToSkiaColor()
            };

            DrawHighlightStrips(canvas, characters, pageSelection.Value, selectionPaint);
        }

        return recorder.EndRecording();
    }

    private void DrawHighlightStrips(SKCanvas canvas, PdfCharacter[] characters, in PdfPanelTextRange range, SKPaint paint)
    {
        PdfRectangle? currentStrip = null;

        foreach (PdfCharacter character in characters.AsSpan(range.StartIndex, range.Length))
        {
            PdfRectangle box = character.BoundingBox;

            if (currentStrip == null)
            {
                currentStrip = box;
            }
            else if (Math.Abs(box.Top - currentStrip.Value.Top) < currentStrip.Value.Height * _parameters.LineMergeThreshold)
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

    private static int? HitTestCharacterNearest(PdfCharacter[] characters, in PdfPoint point, float? maxDistance = null)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < characters.Length; i++)
        {
            PdfRectangle characterBox = characters[i].BoundingBox;
            float dx = point.X - characterBox.MidX;
            float dy = point.Y - characterBox.MidY;
            float distance = (dx * dx) + (dy * dy);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
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

        foreach (SKPicture picture in _textLayerPictures.Values)
        {
            picture.Dispose();
        }

        _textLayerPictures.Clear();
    }
}

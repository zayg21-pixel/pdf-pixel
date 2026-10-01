using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Skia;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Text of the panel: text extraction and text selection.
/// </summary>
public sealed class PdfPanelText
{
    private readonly PdfPageContentProvider _contentProvider;
    private readonly PdfPanelInput _input;
    private readonly PdfPanelGraphics _graphics;
    private readonly StringBuilder _textBuilder = new();
    private readonly bool[] _extractedPages;
    private int _extractedPageCount;
    private int? _anchorPageNumber;
    private int? _anchorCharIndex;
    private int? _currentCharIndex;
    private PdfPanelTextRange? _drawnSelection;
    private PdfColor _drawnSelectionColor;
    private float _drawnLineMergeThreshold;
    private bool _isPointerOverText;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelText"/> with the given content provider and graphics,
    /// and subscribes it to the given input.
    /// </summary>
    internal PdfPanelText(
        PdfPageContentProvider contentProvider,
        PdfPanelInput input,
        PdfPanelGraphics graphics)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _extractedPages = new bool[contentProvider.GetPagesCount()];

        for (int pageNumber = 1; pageNumber <= _extractedPages.Length; pageNumber++)
        {
            MarkExtracted(pageNumber);
        }

        _input.PointerMoved += OnPointerMoved;
        _input.PointerClicked += OnPointerClicked;
        _input.PointerExited += OnPointerExited;
        _input.DragStarted += OnDragStarted;
        _input.DragMoved += OnDragMoved;
        _input.DragEnded += OnDragEnded;
    }

    /// <summary>
    /// Raised when the words of a page have been extracted.
    /// </summary>
    public event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

    /// <summary>
    /// Raised when <see cref="IsTextExtracted"/> becomes <see langword="true"/>.
    /// </summary>
    public event EventHandler? TextExtracted;

    /// <summary>
    /// Whether the words of every page are extracted, not only of the pages that are rendered.
    /// </summary>
    public bool ExtractText { get; set; }

    /// <summary>
    /// Whether the words of every page have been extracted.
    /// </summary>
    public bool IsTextExtracted => _extractedPageCount == _extractedPages.Length;

    /// <summary>
    /// Distance from a character within which the pointer counts as being over it, in unscaled page space.
    /// </summary>
    public float CharacterHitRadius { get; set; } = 10f;

    /// <summary>
    /// Vertical distance between two characters, as a fraction of character height, within which
    /// they highlight as one strip.
    /// </summary>
    public float LineMergeThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Color the selected text is highlighted with.
    /// </summary>
    public PdfColor SelectionColor { get; set; } = new(50f / 255f, 100f / 255f, 220f / 255f, 80f / 255f);

    /// <summary>
    /// 1-based number of the page the selection is on, or <see langword="null"/> when nothing is selected.
    /// </summary>
    public int? SelectionPageNumber { get; set; }

    /// <summary>
    /// Index of the first selected character on <see cref="SelectionPageNumber"/>.
    /// </summary>
    public int SelectionStart { get; set; }

    /// <summary>
    /// Number of selected characters on <see cref="SelectionPageNumber"/>.
    /// </summary>
    public int SelectionLength { get; set; }

    /// <summary>
    /// Whether the pointer is currently over a text character.
    /// </summary>
    public bool IsPointerOverText => _isPointerOverText;

    /// <summary>
    /// The text content of the current selection, or empty if nothing is selected.
    /// </summary>
    public string SelectedText
    {
        get
        {
            PdfPanelTextRange? selection = GetSelection();

            return (selection == null) ? string.Empty : GetText(selection.Value);
        }
    }

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
    /// Clamps the selection to the characters of <see cref="SelectionPageNumber"/> and updates the selection graphics
    /// when the selection or its appearance changed.
    /// </summary>
    internal void Synchronize()
    {
        ClampSelection();

        PdfPanelTextRange? selection = GetSelection();

        if (selection == _drawnSelection
            && SelectionColor.Equals(_drawnSelectionColor)
            && LineMergeThreshold == _drawnLineMergeThreshold)
        {
            return;
        }

        UpdateGraphics(selection);
    }

    /// <summary>
    /// Raises <see cref="PageTextExtracted"/> for the given page, and <see cref="TextExtracted"/> when it was the last page without words.
    /// Updates the selection graphics when the selection is on that page.
    /// </summary>
    internal void OnPageTextExtracted(int pageNumber)
    {
        bool wasTextExtracted = IsTextExtracted;

        MarkExtracted(pageNumber);
        PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber));

        if (!wasTextExtracted && IsTextExtracted)
        {
            TextExtracted?.Invoke(this, EventArgs.Empty);
        }

        if (SelectionPageNumber != pageNumber)
        {
            return;
        }

        ClampSelection();
        UpdateGraphics(GetSelection());
    }

    private void MarkExtracted(int pageNumber)
    {
        if (_extractedPages[pageNumber - 1] || _contentProvider.GetWords(pageNumber) == null)
        {
            return;
        }

        _extractedPages[pageNumber - 1] = true;
        _extractedPageCount++;
    }

    private PdfPanelTextRange? GetSelection()
    {
        if (SelectionPageNumber == null || SelectionLength <= 0)
        {
            return null;
        }

        return new PdfPanelTextRange(SelectionPageNumber.Value, SelectionStart, SelectionLength);
    }

    private void ClampSelection()
    {
        if (SelectionPageNumber == null)
        {
            return;
        }

        PdfWord[]? words = GetWords(SelectionPageNumber.Value);

        if (words == null)
        {
            return;
        }

        PdfWordPart[] lastParts = words[words.Length - 1].Parts;
        PdfWordPart lastPart = lastParts[lastParts.Length - 1];
        int characterCount = lastPart.StartIndex + lastPart.Characters.Length;

        SelectionStart = Math.Max(Math.Min(SelectionStart, characterCount), 0);
        SelectionLength = Math.Max(Math.Min(SelectionLength, characterCount - SelectionStart), 0);
    }

    private void UpdateGraphics(PdfPanelTextRange? selection)
    {
        if (_drawnSelection != null && _drawnSelection.Value.PageNumber != selection?.PageNumber)
        {
            _graphics.Update(_drawnSelection.Value.PageNumber, PdfPanelGraphicsLayer.Selection, null);
        }

        if (selection != null)
        {
            _graphics.Update(selection.Value.PageNumber, PdfPanelGraphicsLayer.Selection, CreateSelectionPicture(selection.Value));
        }

        _drawnSelection = selection;
        _drawnSelectionColor = SelectionColor;
        _drawnLineMergeThreshold = LineMergeThreshold;
    }

    private SKPicture? CreateSelectionPicture(in PdfPanelTextRange selection)
    {
        PdfWord[]? words = GetWords(selection.PageNumber);

        if (words == null)
        {
            return null;
        }

        PdfPanelPageInfo pageInfo = _contentProvider.GetPageInfo(selection.PageNumber);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.CropBox.Width, pageInfo.CropBox.Height));

        using SKPaint selectionPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = SelectionColor.ToSkiaColor()
        };

        PdfPanelHighlightBuilder.DrawHighlight(canvas, words, selection, selectionPaint, LineMergeThreshold);

        return recorder.EndRecording();
    }

    private void OnPointerMoved(object? sender, PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled)
        {
            _isPointerOverText = false;
            return;
        }

        _isPointerOverText = HitTestCharacter(args.Position, CharacterHitRadius) != null;

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

        int? charIndex = HitTestCharacter(args.StartPosition, CharacterHitRadius);

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

        if (SelectionPageNumber == null)
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

        SelectionPageNumber = _anchorPageNumber.Value;
        SelectionStart = start;
        SelectionLength = end - start + 1;
    }

    private void ClearSelection()
    {
        _anchorPageNumber = null;
        _anchorCharIndex = null;
        _currentCharIndex = null;

        SelectionPageNumber = null;
        SelectionStart = 0;
        SelectionLength = 0;
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

    /// <summary>
    /// Returns the characters of <paramref name="words"/> in <paramref name="range"/>.
    /// </summary>
    internal static IEnumerable<PdfCharacter> EnumerateCharacters(PdfWord[] words, PdfPanelTextRange range)
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

    /// <summary>
    /// Unsubscribes from the input.
    /// </summary>
    internal void Dispose()
    {
        _input.PointerMoved -= OnPointerMoved;
        _input.PointerClicked -= OnPointerClicked;
        _input.PointerExited -= OnPointerExited;
        _input.DragStarted -= OnDragStarted;
        _input.DragMoved -= OnDragMoved;
        _input.DragEnded -= OnDragEnded;
    }
}

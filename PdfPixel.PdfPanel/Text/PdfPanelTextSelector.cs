using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Input;
using PdfPixel.Skia;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Tracks text selection state and produces highlight graphics for the selected range.
/// </summary>
public sealed class PdfPanelTextSelector : IDisposable
{
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly PdfPanelTextSelectorParameters _parameters;
    private readonly PdfPanelInputProcessor _processor;
    private readonly Dictionary<int, SKPicture> _selectionPictures = [];
    private int? _anchorPageNumber;
    private int? _anchorCharIndex;
    private int? _currentCharIndex;
    private List<PdfCharacter>? _selectedCharacters;
    private bool _isPointerOverText;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelTextSelector"/> with the given content provider and parameters,
    /// and subscribes it to the given processor.
    /// </summary>
    public PdfPanelTextSelector(
        IPdfPageContentProvider contentProvider,
        PdfPanelTextSelectorParameters parameters,
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
    public string SelectedText
    {
        get
        {
            if (_selectedCharacters == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            foreach (PdfCharacter character in _selectedCharacters)
            {
                if (character.Text != null)
                {
                    builder.Append(character.Text);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Returns the selection highlight picture for the given page, or <see langword="null"/> if that page has no selection.
    /// </summary>
    internal SKPicture? GetSelectionPicture(int pageNumber)
        => (_selectionPictures.TryGetValue(pageNumber, out SKPicture? picture)) ? picture : null;

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

        if (_selectedCharacters == null)
        {
            ClearSelectionPictures();
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

        List<PdfCharacter>? characters = GetCharacters(pagePoint.Value.PageNumber);

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
        int end = Math.Min(Math.Max(_anchorCharIndex.Value, charIndex.Value), characters.Count - 1);
        _selectedCharacters = characters.GetRange(start, end - start + 1);

        UpdateSelectionPicture(_anchorPageNumber.Value);
    }

    private void UpdateSelectionPicture(int pageNumber)
    {
        SKPicture? newPicture = GenerateSelectionPicture(pageNumber);

        if (newPicture == null)
        {
            return;
        }

        if (_selectionPictures.TryGetValue(pageNumber, out SKPicture? oldPicture))
        {
            oldPicture.Dispose();
        }

        _selectionPictures[pageNumber] = newPicture;
    }

    private void ClearSelection()
    {
        ClearSelectionPictures();
        _anchorPageNumber = null;
        _anchorCharIndex = null;
        _currentCharIndex = null;
        _selectedCharacters = null;
    }

    private void ClearSelectionPictures()
    {
        foreach (SKPicture picture in _selectionPictures.Values)
        {
            picture.Dispose();
        }

        _selectionPictures.Clear();
    }

    private List<PdfCharacter>? GetCharacters(int pageNumber)
    {
        PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(pageNumber);

        if (pictures.ContentCharacters == null || pictures.ContentCharacters.Count == 0)
        {
            return null;
        }

        return pictures.ContentCharacters;
    }

    private int? HitTestCharacter(in PdfPanelPointerPosition position, float? maxDistance)
    {
        PdfPanelPagePoint? pagePoint = position.PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        List<PdfCharacter>? characters = GetCharacters(pagePoint.Value.PageNumber);

        if (characters == null)
        {
            return null;
        }

        return HitTestCharacterNearest(characters, pagePoint.Value.Position, maxDistance);
    }

    private SKPicture? GenerateSelectionPicture(int pageNumber)
    {
        if (_anchorPageNumber != pageNumber || _selectedCharacters == null)
        {
            return null;
        }

        PdfPanelPageInfo pageInfo = _contentProvider.GetPageInfo(pageNumber);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.CropBox.Width, pageInfo.CropBox.Height));

        SKPaint highlightPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = _parameters.HighlightColor.ToSkiaColor()
        };

        PdfRectangle? currentStrip = null;

        foreach (PdfCharacter character in _selectedCharacters)
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
                canvas.DrawRect(currentStrip.Value.ToSkRect(), highlightPaint);
                currentStrip = box;
            }
        }

        if (currentStrip != null)
        {
            canvas.DrawRect(currentStrip.Value.ToSkRect(), highlightPaint);
        }

        highlightPaint.Dispose();

        return recorder.EndRecording();
    }

    private static int? HitTestCharacterNearest(List<PdfCharacter> characters, in PdfPoint point, float? maxDistance = null)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < characters.Count; i++)
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

        ClearSelectionPictures();
    }
}

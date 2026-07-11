using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Tracks text selection state and produces highlight graphics for the selected range.
/// </summary>
public sealed partial class PdfPanelTextSelector : IDisposable
{
    private const float MinDragDistance = 4f;
    private const float CharacterHitRadius = 10;
    private const float LineMergeThreshold = 0.5f;

    private static readonly SKColor HighlightColor = new(50, 100, 220, 80);

    private readonly IPdfPageContentProvider _contentProvider;
    private readonly Dictionary<int, SKPicture> _selectionPictures = [];
    private int? _anchorPageNumber;
    private SKPoint _anchorPoint;
    private int? _anchorCharIndex;
    private int? _currentCharIndex;
    private List<PdfCharacter>? _selectedCharacters;
    private PointerPagePosition? _previousPosition;
    private bool _isPointerOverText;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelTextSelector"/> with the given content provider.
    /// </summary>
    public PdfPanelTextSelector(IPdfPageContentProvider contentProvider)
        => _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));

    /// <summary>
    /// Returns the selection highlight picture for the given page, or <see langword="null"/> if that page has no selection.
    /// </summary>
    internal SKPicture? GetSelectionPicture(int pageNumber)
        => (_selectionPictures.TryGetValue(pageNumber, out SKPicture? picture)) ? picture : null;

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
    /// Updates selection state from the current pointer position and regenerates the highlight picture for that page.
    /// </summary>
    internal void Update(PointerPagePosition? position)
    {
        if (position == null)
        {
            _isPointerOverText = false;
            return;
        }

        PointerPagePosition pos = position.Value;

        PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(pos.PageNumber);
        if (pictures.ContentCharacters == null || pictures.ContentCharacters.Count == 0)
        {
            _isPointerOverText = false;
            return;
        }

        List<PdfCharacter> characters = pictures.ContentCharacters;

        _isPointerOverText = HitTestCharacterNearest(characters, pos.Position, CharacterHitRadius) != null;
        UpdateSelectionState(pos, characters);

        SKPicture? newPicture = GenerateSelectionPicture(pos.PageNumber);
        if (newPicture != null)
        {
            if (_selectionPictures.TryGetValue(pos.PageNumber, out SKPicture? oldPicture))
            {
                oldPicture.Dispose();
            }

            _selectionPictures[pos.PageNumber] = newPicture;
        }
    }

    private void UpdateSelectionState(in PointerPagePosition position, List<PdfCharacter> characters)
    {
        PointerPagePosition? previousPosition = _previousPosition;
        _previousPosition = position;

        switch (previousPosition?.State, position.State)
        {
            case (null, PdfPanelButtonState.Default):
            case (PdfPanelButtonState.Default, PdfPanelButtonState.Default):
                break;

            case (null, PdfPanelButtonState.Pressed):
            case (PdfPanelButtonState.Default, PdfPanelButtonState.Pressed):
            {
                OnPointerDown(position, characters);
                break;
            }
            case (PdfPanelButtonState.Pressed, PdfPanelButtonState.Pressed):
            {
                OnPointerDrag(previousPosition.Value, position, characters);
                break;
            }
            case (PdfPanelButtonState.Pressed, PdfPanelButtonState.Default):
            {
                OnPointerDrag(previousPosition.Value, position, characters);
                OnPointerUp();
                break;
            }
        }
    }

    private void OnPointerDown(in PointerPagePosition position, List<PdfCharacter> characters)
    {
        ClearSelectionPictures();
        _anchorPageNumber = null;
        _anchorCharIndex = null;
        _currentCharIndex = null;
        _selectedCharacters = null;

        int? charIndex = HitTestCharacterNearest(characters, position.Position, CharacterHitRadius);
        if (charIndex == null)
        {
            return;
        }

        _anchorPoint = position.Position;
        _anchorPageNumber = position.PageNumber;
        _anchorCharIndex = charIndex.Value;
    }

    private void OnPointerDrag(in PointerPagePosition previousPosition, in PointerPagePosition position, List<PdfCharacter> characters)
    {
        if (_anchorPageNumber != position.PageNumber || _anchorCharIndex == null)
        {
            return;
        }

        float dx = position.Position.X - _anchorPoint.X;
        float dy = position.Position.Y - _anchorPoint.Y;
        if ((dx * dx) + (dy * dy) < MinDragDistance * MinDragDistance)
        {
            return;
        }

        int? charIndex = HitTestCharacterNearest(characters, position.Position);
        if (charIndex != null && charIndex != _currentCharIndex)
        {
            _currentCharIndex = charIndex;

            int start = Math.Max(Math.Min(_anchorCharIndex.Value, charIndex.Value), 0);
            int end = Math.Min(Math.Max(_anchorCharIndex.Value, charIndex.Value), characters.Count - 1);
            _selectedCharacters = characters.GetRange(start, end - start + 1);
        }
    }

    private void OnPointerUp()
    {
        if (_selectedCharacters == null)
        {
            ClearSelectionPictures();
            _anchorPageNumber = null;
        }

        _anchorCharIndex = null;
        _currentCharIndex = null;
    }

    private void ClearSelectionPictures()
    {
        foreach (SKPicture picture in _selectionPictures.Values)
        {
            picture.Dispose();
        }

        _selectionPictures.Clear();
    }

    private static int? HitTestCharacterNearest(List<PdfCharacter> characters, SKPoint point, float? maxDistance = null)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < characters.Count; i++)
        {
            SKRect box = characters[i].BoundingBox;
            float centerX = (box.Left + box.Right) / 2f;
            float centerY = (box.Top + box.Bottom) / 2f;
            float dx = point.X - centerX;
            float dy = point.Y - centerY;
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

    private SKPicture? GenerateSelectionPicture(int pageNumber)
    {
        if (_anchorPageNumber != pageNumber || _selectedCharacters == null)
        {
            return null;
        }

        PdfPanelPageInfo pageInfo = _contentProvider.GetPageInfo(pageNumber);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.Width, pageInfo.Height));

        SKPaint highlightPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = HighlightColor
        };

        SKRect? currentStrip = null;

        foreach (PdfCharacter character in _selectedCharacters)
        {
            SKRect box = character.BoundingBox;

            if (currentStrip == null)
            {
                currentStrip = box;
            }
            else if (Math.Abs(box.Top - currentStrip.Value.Top) < currentStrip.Value.Height * LineMergeThreshold)
            {
                currentStrip = SKRect.Union(currentStrip.Value, box);
            }
            else
            {
                canvas.DrawRect(currentStrip.Value, highlightPaint);
                currentStrip = box;
            }
        }

        if (currentStrip != null)
        {
            canvas.DrawRect(currentStrip.Value, highlightPaint);
        }

        highlightPaint.Dispose();

        return recorder.EndRecording();
    }

    /// <inheritdoc />
    public void Dispose() => ClearSelectionPictures();
}

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
    private readonly IPdfPageContentProvider _contentProvider;
    private readonly Dictionary<int, List<PdfCharacter>> _flattenedPages = [];
    private SKPicture? _picture;
    private int? _anchorPageNumber;
    private int _anchorCharIndex;
    private int _currentCharIndex;
    private bool _isDragging;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelTextSelector"/> with the given content provider.
    /// </summary>
    public PdfPanelTextSelector(IPdfPageContentProvider contentProvider)
        => _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));

    /// <summary>
    /// The currently generated selection highlight picture, or <see langword="null"/> if nothing is selected.
    /// </summary>
    internal SKPicture? Picture => _picture;

    /// <summary>
    /// The text content of the current selection, or empty if nothing is selected.
    /// </summary>
    public string SelectedText
    {
        get
        {
            if (_anchorPageNumber == null)
            {
                return string.Empty;
            }

            if (!_flattenedPages.TryGetValue(_anchorPageNumber.Value, out List<PdfCharacter>? characters))
            {
                return string.Empty;
            }

            int start = Math.Min(_anchorCharIndex, _currentCharIndex);
            int end = Math.Max(_anchorCharIndex, _currentCharIndex);
            start = Math.Max(start, 0);
            end = Math.Min(end, characters.Count - 1);

            StringBuilder builder = new();
            for (int i = start; i <= end; i++)
            {
                if (characters[i].Text != null)
                {
                    builder.Append(characters[i].Text);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Updates selection state from the current pointer position and regenerates the highlight picture.
    /// </summary>
    internal void Update(PointerPagePosition? position)
    {
        _picture?.Dispose();
        _picture = null;

        if (position == null)
        {
            return;
        }

        PointerPagePosition pos = position.Value;
        PdfContentPictures pictures = _contentProvider.GetExistingContentPictures(pos.PageNumber);
        if (pictures.ContentRootTextBlock == null)
        {
            return;
        }

        List<PdfCharacter> characters = GetFlattenedCharacters(pos.PageNumber, pictures.ContentRootTextBlock);
        if (characters.Count == 0)
        {
            return;
        }

        UpdateSelectionState(pos, characters);

        if (_anchorPageNumber != null)
        {
            _picture = GenerateSelectionPicture(pos.PageNumber, characters);
        }
    }

    private void UpdateSelectionState(in PointerPagePosition position, List<PdfCharacter> characters)
    {
        int charIndex = HitTestCharacter(characters, position.Position);

        if (position.State == PdfPanelButtonState.Pressed)
        {
            if (!_isDragging)
            {
                _anchorPageNumber = position.PageNumber;
                _anchorCharIndex = charIndex;
                _currentCharIndex = charIndex;
                _isDragging = true;
            }
            else if (_anchorPageNumber == position.PageNumber)
            {
                _currentCharIndex = charIndex;
            }
        }
        else
        {
            _isDragging = false;
        }
    }

    private static int HitTestCharacter(List<PdfCharacter> characters, SKPoint point)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < characters.Count; i++)
        {
            SKRect box = characters[i].BoundingBox;

            if (box.Contains(point))
            {
                return i;
            }

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

        return closestIndex;
    }

    private SKPicture? GenerateSelectionPicture(int pageNumber, List<PdfCharacter> characters)
    {
        if (_anchorPageNumber != pageNumber)
        {
            return null;
        }

        int start = Math.Min(_anchorCharIndex, _currentCharIndex);
        int end = Math.Max(_anchorCharIndex, _currentCharIndex);
        start = Math.Max(start, 0);
        end = Math.Min(end, characters.Count - 1);

        if (start > end)
        {
            return null;
        }

        PdfPanelPageInfo pageInfo = _contentProvider.GetPageInfo(pageNumber);

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.Width, pageInfo.Height));

        SKPaint highlightPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(50, 100, 220, 80)
        };

        SKRect? currentStrip = null;

        for (int i = start; i <= end; i++)
        {
            SKRect box = characters[i].BoundingBox;

            if (currentStrip == null)
            {
                currentStrip = box;
            }
            else if (Math.Abs(box.Top - currentStrip.Value.Top) < currentStrip.Value.Height * 0.5f)
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
    public void Dispose()
    {
        _picture?.Dispose();
        _picture = null;
        _flattenedPages.Clear();
    }
}

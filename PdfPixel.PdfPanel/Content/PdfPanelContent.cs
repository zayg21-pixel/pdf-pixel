using PdfPixel.Color;
using PdfPixel.Objects;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Skia;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Content;

/// <summary>
/// Content of the panel: the content item under the pointer and image selection.
/// </summary>
public sealed class PdfPanelContent
{
    internal const float DefaultSelectionBorderThickness = 2f;
    internal const bool DefaultAllowHover = true;
    internal const bool DefaultAllowImageSelection = true;

    internal static readonly PdfColor DefaultSelectionBorderColor = new(0f, 0f, 0f, 1f);

    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelInput _input;
    private readonly PdfPanelGraphics _graphics;
    private PdfPanelContentItem? _activeNamedContent;
    private PdfPanelImageContentItem? _drawnSelection;
    private PdfColor _drawnBorderColor;
    private float _drawnBorderThickness;
    private float _drawnScale;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelContent"/> with the given panel document and graphics,
    /// and subscribes it to the given input.
    /// </summary>
    internal PdfPanelContent(
        PdfPanelDocument panelDocument,
        PdfPanelInput input,
        PdfPanelGraphics graphics)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));

        _input.PointerMoved += OnPointerMoved;
        _input.PointerPressed += OnPointerPressed;
        _input.PointerReleased += OnPointerReleased;
        _input.PointerClicked += OnPointerClicked;
        _input.PointerExited += OnPointerExited;
        _input.DragStarted += OnDragStarted;
    }

    /// <summary>
    /// Width of the border drawn around the selected content, in device pixels.
    /// </summary>
    public float SelectionBorderThickness { get; set; } = DefaultSelectionBorderThickness;

    /// <summary>
    /// Color of the border drawn around the selected content.
    /// </summary>
    public PdfColor SelectionBorderColor { get; set; } = DefaultSelectionBorderColor;

    /// <summary>
    /// Whether the content under the pointer is tracked.
    /// </summary>
    public bool AllowHover { get; set; } = DefaultAllowHover;

    /// <summary>
    /// Whether images can be selected with the pointer.
    /// </summary>
    public bool AllowImageSelection { get; set; } = DefaultAllowImageSelection;

    /// <summary>
    /// Content item under the pointer, or <see langword="null"/> if there is none.
    /// </summary>
    public PdfPanelContentItem? ActiveContent { get; private set; }

    /// <summary>
    /// Alternate description of the content under the pointer, or <see langword="null"/> if there is none.
    /// </summary>
    public string? ActiveContentName { get; private set; }

    /// <summary>
    /// Selected image, or <see langword="null"/> when no image is selected.
    /// </summary>
    public PdfPanelImageContentItem? SelectedImage { get; private set; }

    /// <summary>
    /// Updates the selection graphics when the selection, its appearance or <paramref name="scale"/> changed.
    /// </summary>
    internal void Synchronize(float scale)
    {
        if (!AllowHover)
        {
            ClearActiveContent();
        }

        if (!AllowImageSelection)
        {
            SelectedImage = null;
        }

        if (SelectedImage == _drawnSelection
            && SelectionBorderColor.Equals(_drawnBorderColor)
            && SelectionBorderThickness == _drawnBorderThickness
            && scale == _drawnScale)
        {
            return;
        }

        UpdateGraphics(SelectedImage, scale);
    }

    private void UpdateGraphics(PdfPanelImageContentItem? selection, float scale)
    {
        if (_drawnSelection != null && _drawnSelection.PageNumber != selection?.PageNumber)
        {
            _graphics.Update(_drawnSelection.PageNumber, PdfPanelGraphicsLayer.ContentSelection, null);
        }

        if (selection != null)
        {
            _graphics.Update(selection.PageNumber, PdfPanelGraphicsLayer.ContentSelection, CreateSelectionPicture(selection, scale));
        }

        _drawnSelection = selection;
        _drawnBorderColor = SelectionBorderColor;
        _drawnBorderThickness = SelectionBorderThickness;
        _drawnScale = scale;
    }

    private SKPicture CreateSelectionPicture(PdfPanelImageContentItem selection, float scale)
    {
        PdfPanelPage page = _panelDocument.Pages[selection.PageNumber - 1];

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(SKRect.Create(page.Size.Width, page.Size.Height));

        using SKPaint borderPaint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = SelectionBorderThickness / scale,
            Color = SelectionBorderColor.ToSkiaColor(),
            IsAntialias = true
        };

        canvas.DrawRect(selection.BoundingBox.ToSkRect(), borderPaint);

        return recorder.EndRecording();
    }

    private void OnPointerMoved(object? sender, PdfPanelPointerEventArgs args) => UpdateActiveContent(args);

    private void OnPointerPressed(object? sender, PdfPanelPointerEventArgs args)
    {
        if (SelectedImage != null && HitTestImage(args) != SelectedImage)
        {
            SelectedImage = null;
        }

        UpdateActiveContent(args);
    }

    private void OnPointerReleased(object? sender, PdfPanelPointerEventArgs args) => UpdateActiveContent(args);

    private void OnPointerClicked(object? sender, PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled || !AllowImageSelection)
        {
            return;
        }

        PdfPanelImageContentItem? image = HitTestImage(args);

        if (image == null)
        {
            return;
        }

        args.IsHandled = true;
        SelectedImage = image;
    }

    private void OnDragStarted(object? sender, PdfPanelDragEventArgs args) => ClearActiveContent();

    private void OnPointerExited(object? sender, EventArgs args) => ClearActiveContent();

    private void UpdateActiveContent(PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled || !AllowHover)
        {
            ClearActiveContent();
            return;
        }

        ActiveContent = HitTestContent(args, requireAlternateDescription: false);
        UpdateActiveNamedContent(HitTestContent(args, requireAlternateDescription: true));
    }

    private void UpdateActiveNamedContent(PdfPanelContentItem? namedContent)
    {
        if (namedContent == _activeNamedContent)
        {
            return;
        }

        _activeNamedContent = namedContent;
        ActiveContentName = namedContent?.Entry.AlternateDescription?.DecodePdfString();
    }

    private void ClearActiveContent()
    {
        ActiveContent = null;
        UpdateActiveNamedContent(null);
    }

    private PdfPanelContentItem? HitTestContent(PdfPanelPointerEventArgs args, bool requireAlternateDescription)
    {
        PdfPanelPagePoint? pagePoint = args.Position.PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        PdfPanelContentItem[]? contentItems = _panelDocument.GetContent(pagePoint.Value.PageNumber);

        if (contentItems == null)
        {
            return null;
        }

        for (int itemIndex = contentItems.Length - 1; itemIndex >= 0; itemIndex--)
        {
            PdfPanelContentItem contentItem = contentItems[itemIndex];

            if ((!requireAlternateDescription || contentItem.Entry.AlternateDescription != null)
                && contentItem.BoundingBox.Contains(pagePoint.Value.Position))
            {
                return contentItem;
            }
        }

        return null;
    }

    private PdfPanelImageContentItem? HitTestImage(PdfPanelPointerEventArgs args)
    {
        PdfPanelPagePoint? pagePoint = args.Position.PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        PdfPanelContentItem[]? contentItems = _panelDocument.GetContent(pagePoint.Value.PageNumber);

        if (contentItems == null)
        {
            return null;
        }

        for (int itemIndex = contentItems.Length - 1; itemIndex >= 0; itemIndex--)
        {
            if (contentItems[itemIndex] is PdfPanelImageContentItem image
                && image.BoundingBox.Contains(pagePoint.Value.Position))
            {
                return image;
            }
        }

        return null;
    }

    /// <summary>
    /// Unsubscribes from the input.
    /// </summary>
    internal void Dispose()
    {
        _input.PointerMoved -= OnPointerMoved;
        _input.PointerPressed -= OnPointerPressed;
        _input.PointerReleased -= OnPointerReleased;
        _input.PointerClicked -= OnPointerClicked;
        _input.PointerExited -= OnPointerExited;
        _input.DragStarted -= OnDragStarted;
    }
}

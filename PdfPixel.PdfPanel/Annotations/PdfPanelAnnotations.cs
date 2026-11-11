using PdfPixel.Annotations;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Tracks the annotation under the pointer, reports clicks on it and draws the bubbles of the visible pages.
/// </summary>
public sealed class PdfPanelAnnotations
{
    private readonly PdfPanelContext _context;
    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelInput _input;
    private readonly PdfPanelGraphics _graphics;
    private readonly Dictionary<int, BubblesKey> _drawnBubbles = [];
    private bool _isOverActiveContent;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelAnnotations"/> and subscribes it to the given input.
    /// </summary>
    internal PdfPanelAnnotations(PdfPanelContext context, PdfPanelDocument panelDocument, PdfPanelInput input, PdfPanelGraphics graphics)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
    /// Annotation under the pointer, or <see langword="null"/> if there is none.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation { get; private set; }

    /// <summary>
    /// Annotation clicked during the last <see cref="PdfPanelContext.Synchronize"/>, or <see langword="null"/> if none was clicked.
    /// </summary>
    public PdfAnnotationPopup? ClickedAnnotation { get; private set; }

    /// <summary>
    /// Interaction state of <see cref="ActiveAnnotation"/>.
    /// </summary>
    public PdfPanelPointerState ActiveAnnotationState { get; private set; }

    /// <summary>
    /// Clears <see cref="ClickedAnnotation"/>.
    /// </summary>
    internal void ClearClicked() => ClickedAnnotation = null;

    /// <summary>
    /// Returns the states the annotations of <paramref name="pages"/> are drawn in at the given scale and the scale of a
    /// zoom of 100%, with the active annotation in its pointer state.
    /// </summary>
    internal PdfAnnotationStates CreateStates(RequestPageInfo[] pages, float scale, float referenceScale)
    {
        Dictionary<PdfAnnotation, PdfAnnotationState> states = [];

        foreach (RequestPageInfo page in pages)
        {
            IReadOnlyList<PdfPageAnnotation> pageAnnotations;

            lock (page.Page.PanelDocument.DocumentLocker)
            {
                pageAnnotations = page.Page.PdfPage.GetAnnotations();
            }

            foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
            {
                bool isActive = pageAnnotation == ActiveAnnotation?.PageAnnotation;
                PdfAnnotationState state = pageAnnotation.Content.GetState(isActive, isActive && _isOverActiveContent, ActiveAnnotationState, scale, referenceScale, page.Rotation);

                if (state != PdfAnnotationState.Default)
                {
                    states[pageAnnotation.Content] = state;
                }
            }
        }

        return new PdfAnnotationStates(states);
    }

    /// <summary>
    /// Records again the bubbles of every page of <paramref name="request"/> whose scale, rotation or active annotation
    /// changed, and removes the bubbles of the pages no longer in it.
    /// </summary>
    internal void UpdateBubbles(PagesDrawingRequest request)
    {
        HashSet<int> requestedPageNumbers = [];

        foreach (RequestPageInfo page in request.Pages)
        {
            requestedPageNumbers.Add(page.PageNumber);

            PdfAnnotationPopup? activeAnnotation = GetActiveAnnotation(page.PageNumber);
            var activeAnnotationState = PdfPanelPointerState.None;

            if (activeAnnotation != null)
            {
                activeAnnotationState = ActiveAnnotationState;
            }

            BubblesKey key = new(request.Scale, request.ReferenceScale, page.Rotation, activeAnnotation, activeAnnotationState, activeAnnotation != null && _isOverActiveContent);

            if (_drawnBubbles.TryGetValue(page.PageNumber, out BubblesKey drawnKey) && drawnKey.Equals(key))
            {
                continue;
            }

            _graphics.Update(page.PageNumber, PdfPanelGraphicsLayer.Bubbles, CreateBubblesRecording(page, request));
            _drawnBubbles[page.PageNumber] = key;
        }

        List<int> removedPageNumbers = [];

        foreach (int pageNumber in _drawnBubbles.Keys)
        {
            if (!requestedPageNumbers.Contains(pageNumber))
            {
                removedPageNumbers.Add(pageNumber);
            }
        }

        foreach (int pageNumber in removedPageNumbers)
        {
            _graphics.Update(pageNumber, PdfPanelGraphicsLayer.Bubbles, null);
            _drawnBubbles.Remove(pageNumber);
        }
    }

    /// <summary>
    /// Returns the active annotation when it lies on the given page, otherwise <see langword="null"/>.
    /// </summary>
    private PdfAnnotationPopup? GetActiveAnnotation(int pageNumber)
    {
        if (ActiveAnnotation?.PageAnnotation.Page.PageNumber == pageNumber)
        {
            return ActiveAnnotation;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Records the bubbles of the visible annotations of <paramref name="page"/> that display one, screen-fixed at the
    /// scale and rotation of <paramref name="request"/>; <see langword="null"/> when the page has none.
    /// </summary>
    private PdfCommandRecorder? CreateBubblesRecording(in RequestPageInfo page, PagesDrawingRequest request)
    {
        IPdfPage pdfPage = page.Page.PdfPage;
        PdfCommandRecorder? recorder = null;
        IReadOnlyList<PdfPageAnnotation> pageAnnotations;

        lock (page.Page.PanelDocument.DocumentLocker)
        {
            pageAnnotations = pdfPage.GetAnnotations();
        }

        foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
        {
            PdfAnnotation annotation = pageAnnotation.Content;
            bool isActive = pageAnnotation == ActiveAnnotation?.PageAnnotation;

            if (!annotation.ShouldDisplayBubble() || !annotation.IsVisible(isActive && _isOverActiveContent))
            {
                continue;
            }

            if (recorder == null)
            {
                recorder = new PdfCommandRecorder();
                recorder.ApplyPageTransformations(pdfPage.CropBox, scale: pdfPage.UserUnit);
            }

            var visualState = PdfAnnotationVisualStateKind.Normal;

            if (isActive)
            {
                visualState = PdfAnnotationExtensions.GetVisualState(ActiveAnnotationState);
            }

            PdfPoint anchor = annotation.ContentStart;

            recorder.Process(SaveStateCommand.Instance);
            recorder.Process(new ConcatMatrixCommand(annotation.GetDisplayMatrix(request.Scale, request.ReferenceScale, page.Rotation)));
            recorder.Process(new ConcatMatrixCommand(annotation.GetBubbleMatrix(anchor, request.Scale, request.ReferenceScale, page.Rotation, pdfPage.CropBox)));
            PdfAnnotationExtensions.RenderBubble(recorder, anchor, visualState);
            recorder.Process(RestoreStateCommand.Instance);
        }

        return recorder;
    }

    private void OnPointerMoved(object? sender, PdfPanelPointerEventArgs args) => UpdateActiveAnnotation(args);

    private void OnPointerPressed(object? sender, PdfPanelPointerEventArgs args) => UpdateActiveAnnotation(args);

    private void OnPointerReleased(object? sender, PdfPanelPointerEventArgs args) => UpdateActiveAnnotation(args);

    private void OnPointerClicked(object? sender, PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled)
        {
            return;
        }

        PdfAnnotationPopup? annotation = HitTest(args);

        if (annotation == null || !annotation.PageAnnotation.Content.IsVisible(isActive: true))
        {
            return;
        }

        args.IsHandled = true;
        ClickedAnnotation = annotation;
    }

    private void OnDragStarted(object? sender, PdfPanelDragEventArgs args) => Clear();

    private void OnPointerExited(object? sender, EventArgs args) => Clear();

    private void UpdateActiveAnnotation(PdfPanelPointerEventArgs args)
    {
        if (args.IsHandled)
        {
            Clear();
            return;
        }

        PdfAnnotationPopup? annotation = HitTest(args);

        ActiveAnnotation = annotation;
        ActiveAnnotationState = GetPointerState(annotation);
        _isOverActiveContent = false;

        if (annotation == null)
        {
            return;
        }

        _isOverActiveContent = IsOverContent(annotation, args);
        args.IsHandled = true;
        args.Cursor = annotation.PageAnnotation.Content.GetCursor();
    }

    private PdfPanelPointerState GetPointerState(PdfAnnotationPopup? annotation)
    {
        if (annotation == null)
        {
            return PdfPanelPointerState.None;
        }

        return (_input.ButtonState == PdfPanelButtonState.Pressed)
            ? PdfPanelPointerState.Pressed
            : PdfPanelPointerState.Hovered;
    }

    private PdfAnnotationPopup? HitTest(PdfPanelPointerEventArgs args)
    {
        PdfPanelPagePoint? pagePoint = args.Position.PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        return _panelDocument.GetAnnotationPopupAt(pagePoint.Value.PageNumber, pagePoint.Value.Position, _context.Scale, _context.ReferenceScale);
    }

    /// <summary>
    /// Returns whether the pointer of <paramref name="args"/> is over <paramref name="annotation"/> itself rather than its
    /// bubble.
    /// </summary>
    private bool IsOverContent(PdfAnnotationPopup annotation, PdfPanelPointerEventArgs args)
    {
        PdfPanelPagePoint? pagePoint = args.Position.PagePoint;

        if (pagePoint == null)
        {
            return false;
        }

        PdfPoint pdfPoint = _panelDocument.Pages[pagePoint.Value.PageNumber - 1].ToPdfPoint(pagePoint.Value.Position);

        return annotation.IsOverContent(pdfPoint, _context.Scale, _context.ReferenceScale);
    }

    private void Clear()
    {
        ActiveAnnotation = null;
        ActiveAnnotationState = PdfPanelPointerState.None;
        _isOverActiveContent = false;
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

    /// <summary>
    /// What the bubbles of a page were recorded for: the scale, the scale of a zoom of 100%, the page rotation and the
    /// active annotation on the page with its pointer state and whether the pointer is over its content.
    /// </summary>
    private readonly struct BubblesKey : IEquatable<BubblesKey>
    {
        private readonly float _scale;
        private readonly float _referenceScale;
        private readonly int _rotation;
        private readonly PdfAnnotationPopup? _activeAnnotation;
        private readonly PdfPanelPointerState _activeAnnotationState;
        private readonly bool _isOverActiveContent;

        public BubblesKey(
            float scale,
            float referenceScale,
            int rotation,
            PdfAnnotationPopup? activeAnnotation,
            PdfPanelPointerState activeAnnotationState,
            bool isOverActiveContent)
        {
            _scale = scale;
            _referenceScale = referenceScale;
            _rotation = rotation;
            _activeAnnotation = activeAnnotation;
            _activeAnnotationState = activeAnnotationState;
            _isOverActiveContent = isOverActiveContent;
        }

        public bool Equals(BubblesKey other)
        {
            return _scale == other._scale
                && _referenceScale == other._referenceScale
                && _rotation == other._rotation
                && ReferenceEquals(_activeAnnotation, other._activeAnnotation)
                && _activeAnnotationState == other._activeAnnotationState
                && _isOverActiveContent == other._isOverActiveContent;
        }

        public override bool Equals(object? obj) => obj is BubblesKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(_scale, _referenceScale, _rotation, _activeAnnotation, _activeAnnotationState, _isOverActiveContent);
    }
}

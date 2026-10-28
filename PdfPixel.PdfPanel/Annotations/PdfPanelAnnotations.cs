using PdfPixel.PdfPanel.Input;
using System;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Tracks the annotation under the pointer and reports clicks on it.
/// </summary>
public sealed class PdfPanelAnnotations
{
    private readonly PdfPanelContext _context;
    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelInput _input;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelAnnotations"/> and subscribes it to the given input.
    /// </summary>
    internal PdfPanelAnnotations(PdfPanelContext context, PdfPanelDocument panelDocument, PdfPanelInput input)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _input = input ?? throw new ArgumentNullException(nameof(input));

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

        if (annotation == null)
        {
            return;
        }

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

    private void Clear()
    {
        ActiveAnnotation = null;
        ActiveAnnotationState = PdfPanelPointerState.None;
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

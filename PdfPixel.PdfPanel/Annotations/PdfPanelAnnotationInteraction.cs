using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Input;
using System;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Tracks the annotation under the pointer and reports clicks on it.
/// </summary>
public sealed class PdfPanelAnnotationInteraction : IDisposable
{
    private readonly PdfPanelPageCollection _pages;
    private readonly PdfPanelInputProcessor _processor;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelAnnotationInteraction"/> and subscribes it to the given processor.
    /// </summary>
    public PdfPanelAnnotationInteraction(PdfPanelPageCollection pages, PdfPanelInputProcessor processor)
    {
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));

        _processor.PointerMoved += OnPointerMoved;
        _processor.PointerPressed += OnPointerPressed;
        _processor.PointerReleased += OnPointerReleased;
        _processor.PointerClicked += OnPointerClicked;
        _processor.PointerExited += OnPointerExited;
        _processor.DragStarted += OnDragStarted;
    }

    /// <summary>
    /// Annotation under the pointer, or <see langword="null"/> if there is none.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation { get; private set; }

    /// <summary>
    /// Annotation clicked since the last <see cref="ClearClicked"/>, or <see langword="null"/> if there is none.
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

        if (annotation == null)
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
        args.Cursor = GetCursor(annotation);
    }

    private PdfPanelPointerState GetPointerState(PdfAnnotationPopup? annotation)
    {
        if (annotation == null)
        {
            return PdfPanelPointerState.None;
        }

        return (_processor.ButtonState == PdfPanelButtonState.Pressed)
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

        return _pages.GetAnnotationPopupAt(pagePoint.Value.PageNumber, pagePoint.Value.Position);
    }

    private void Clear()
    {
        ActiveAnnotation = null;
        ActiveAnnotationState = PdfPanelPointerState.None;
    }

    private static PdfPanelCursor GetCursor(PdfAnnotationPopup annotation)
    {
        PdfAnnotationCursorType? cursorType = annotation.PageAnnotation?.Content.CursorType;

        if (cursorType == null)
        {
            return PdfPanelCursor.Hand;
        }

        return cursorType.Value switch
        {
            PdfAnnotationCursorType.Hand => PdfPanelCursor.Hand,
            PdfAnnotationCursorType.IBeam => PdfPanelCursor.IBeam,
            _ => PdfPanelCursor.Arrow
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _processor.PointerMoved -= OnPointerMoved;
        _processor.PointerPressed -= OnPointerPressed;
        _processor.PointerReleased -= OnPointerReleased;
        _processor.PointerClicked -= OnPointerClicked;
        _processor.PointerExited -= OnPointerExited;
        _processor.DragStarted -= OnDragStarted;
    }
}

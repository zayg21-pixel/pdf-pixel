using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Turns pointer and key input into pointer, click and drag events.
/// </summary>
public sealed class PdfPanelInput
{
    private PdfPanelPointerPosition? _pressPosition;
    private PdfPanelPointerPosition? _lastPosition;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelInput"/>.
    /// </summary>
    internal PdfPanelInput()
    {
    }

    /// <summary>
    /// Occurs when the pointer button is pressed.
    /// </summary>
    public event EventHandler<PdfPanelPointerEventArgs>? PointerPressed;

    /// <summary>
    /// Occurs when the pointer moves while no drag is in progress.
    /// </summary>
    public event EventHandler<PdfPanelPointerEventArgs>? PointerMoved;

    /// <summary>
    /// Occurs when the pointer button is released.
    /// </summary>
    public event EventHandler<PdfPanelPointerEventArgs>? PointerReleased;

    /// <summary>
    /// Occurs when the pointer is released without having travelled
    /// <see cref="MinimumDragDistance"/> from the press position.
    /// </summary>
    public event EventHandler<PdfPanelPointerEventArgs>? PointerClicked;

    /// <summary>
    /// Occurs when the pointer travels <see cref="MinimumDragDistance"/> while pressed.
    /// </summary>
    public event EventHandler<PdfPanelDragEventArgs>? DragStarted;

    /// <summary>
    /// Occurs when the pointer moves while a drag is in progress.
    /// </summary>
    public event EventHandler<PdfPanelDragEventArgs>? DragMoved;

    /// <summary>
    /// Occurs when a drag in progress ends.
    /// </summary>
    public event EventHandler<PdfPanelDragEventArgs>? DragEnded;

    /// <summary>
    /// Occurs when the pointer leaves the panel.
    /// </summary>
    public event EventHandler? PointerExited;

    /// <summary>
    /// Occurs when a key is pressed.
    /// </summary>
    public event EventHandler<PdfPanelKeyEventArgs>? KeyPressed;

    /// <summary>
    /// Current pointer position in panel coordinates, or null if pointer is not over the panel.
    /// </summary>
    public PdfPoint? PointerPosition { get; set; }

    /// <summary>
    /// Current pointer button state.
    /// </summary>
    public PdfPanelButtonState PointerState { get; set; }

    /// <summary>
    /// Distance the pointer travels from the press position before a press becomes a drag, in panel pixels.
    /// </summary>
    public float MinimumDragDistance { get; set; } = 4f;

    /// <summary>
    /// Cursor shape the last pointer event resolved to.
    /// </summary>
    public PdfPanelCursor Cursor { get; private set; }

    /// <summary>
    /// Whether a drag is in progress.
    /// </summary>
    public bool IsDragging { get; private set; }

    /// <summary>
    /// Pointer button state of the last report.
    /// </summary>
    internal PdfPanelButtonState ButtonState { get; private set; }

    /// <summary>
    /// Reports a key press with the modifiers held at the time.
    /// </summary>
    public void PressKey(PdfPanelKey key, PdfPanelKeyModifiers modifiers)
    {
        PdfPanelKeyEventArgs args = new(key, modifiers);
        KeyPressed?.Invoke(this, args);
    }

    /// <summary>
    /// Reports <paramref name="position"/> with <see cref="PointerState"/>, raising the events
    /// for the transition from the previous report. A <see langword="null"/> position reports the pointer leaving the panel.
    /// </summary>
    internal void Synchronize(PdfPanelPointerPosition? position)
    {
        if (position == null)
        {
            Leave();
            return;
        }

        Update(position.Value, PointerState);
    }

    /// <summary>
    /// Reports the pointer leaving the panel, cancelling a press or drag in progress.
    /// </summary>
    internal void Leave()
    {
        if (_lastPosition == null)
        {
            return;
        }

        Cancel();

        _lastPosition = null;
        Cursor = PdfPanelCursor.Arrow;

        PointerExited?.Invoke(this, EventArgs.Empty);
    }

    private void Update(in PdfPanelPointerPosition position, PdfPanelButtonState buttonState)
    {
        PdfPanelButtonState previousState = ButtonState;
        ButtonState = buttonState;

        if (buttonState == PdfPanelButtonState.Pressed && previousState == PdfPanelButtonState.Default)
        {
            Press(position);
            return;
        }

        if (buttonState == PdfPanelButtonState.Default && previousState == PdfPanelButtonState.Pressed)
        {
            Release(position);
            return;
        }

        if (_lastPosition == null || _lastPosition.Value != position)
        {
            Move(position);
        }
    }

    private void Cancel()
    {
        PdfPanelPointerPosition? pressPosition = _pressPosition;
        bool wasDragging = IsDragging;

        _pressPosition = null;
        IsDragging = false;
        ButtonState = PdfPanelButtonState.Default;

        if (!wasDragging || pressPosition == null || _lastPosition == null)
        {
            return;
        }

        PdfPanelDragEventArgs dragArgs = new(pressPosition.Value, _lastPosition.Value);
        DragEnded?.Invoke(this, dragArgs);
    }

    private void Press(in PdfPanelPointerPosition position)
    {
        _pressPosition = position;
        _lastPosition = position;
        IsDragging = false;

        PdfPanelPointerEventArgs args = new(position);
        PointerPressed?.Invoke(this, args);
    }

    private void Move(in PdfPanelPointerPosition position)
    {
        _lastPosition = position;

        if (_pressPosition != null)
        {
            PdfPanelPointerPosition pressPosition = _pressPosition.Value;

            if (!IsDragging && HasTravelledDragDistance(pressPosition, position))
            {
                IsDragging = true;

                PdfPanelDragEventArgs startArgs = new(pressPosition, position);
                DragStarted?.Invoke(this, startArgs);
                Cursor = startArgs.Cursor;
                return;
            }

            if (IsDragging)
            {
                PdfPanelDragEventArgs moveArgs = new(pressPosition, position);
                DragMoved?.Invoke(this, moveArgs);
                Cursor = moveArgs.Cursor;
                return;
            }
        }

        PdfPanelPointerEventArgs args = new(position);
        PointerMoved?.Invoke(this, args);
        Cursor = args.Cursor;
    }

    private void Release(in PdfPanelPointerPosition position)
    {
        PdfPanelPointerPosition? pressPosition = _pressPosition;
        bool wasDragging = IsDragging;

        _pressPosition = null;
        _lastPosition = position;
        IsDragging = false;

        PdfPanelPointerEventArgs releasedArgs = new(position);
        PointerReleased?.Invoke(this, releasedArgs);

        if (pressPosition == null)
        {
            return;
        }

        if (wasDragging)
        {
            PdfPanelDragEventArgs dragArgs = new(pressPosition.Value, position);
            DragEnded?.Invoke(this, dragArgs);
            return;
        }

        PdfPanelPointerEventArgs clickArgs = new(pressPosition.Value);
        PointerClicked?.Invoke(this, clickArgs);
    }

    private bool HasTravelledDragDistance(in PdfPanelPointerPosition pressPosition, in PdfPanelPointerPosition position)
    {
        float deltaX = position.PanelPosition.X - pressPosition.PanelPosition.X;
        float deltaY = position.PanelPosition.Y - pressPosition.PanelPosition.Y;

        return (deltaX * deltaX) + (deltaY * deltaY)
            >= MinimumDragDistance * MinimumDragDistance;
    }
}

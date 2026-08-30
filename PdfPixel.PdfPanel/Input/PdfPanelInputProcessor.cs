using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Turns pointer and key input into pointer, click and drag events.
/// </summary>
public sealed class PdfPanelInputProcessor
{
    private readonly PdfPanelInputParameters _parameters;
    private PdfPanelPointerPosition? _pressPosition;
    private PdfPanelPointerPosition? _lastPosition;

    /// <summary>
    /// Initializes a new <see cref="PdfPanelInputProcessor"/> with the given parameters.
    /// </summary>
    public PdfPanelInputProcessor(PdfPanelInputParameters parameters)
        => _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

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
    /// <see cref="PdfPanelInputParameters.MinimumDragDistance"/> from the press position.
    /// </summary>
    public event EventHandler<PdfPanelPointerEventArgs>? PointerClicked;

    /// <summary>
    /// Occurs when the pointer travels <see cref="PdfPanelInputParameters.MinimumDragDistance"/> while pressed.
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
    public PdfPanelButtonState ButtonState { get; private set; }

    /// <summary>
    /// Position of the last report, or <see langword="null"/> when the pointer is outside the panel.
    /// </summary>
    public PdfPanelPointerPosition? PointerPosition => _lastPosition;

    /// <summary>
    /// Reports the pointer position and button state, raising the events
    /// for the transition from the previous report.
    /// </summary>
    public void Update(in PdfPanelPointerPosition position, PdfPanelButtonState buttonState)
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

    /// <summary>
    /// Ends a press or drag in progress without raising <see cref="PointerReleased"/> or <see cref="PointerClicked"/>.
    /// </summary>
    public void Cancel()
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

    /// <summary>
    /// Reports the pointer leaving the panel, cancelling a press or drag in progress.
    /// </summary>
    public void Leave()
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

    /// <summary>
    /// Reports a key press with the modifiers held at the time.
    /// </summary>
    public void PressKey(PdfPanelKey key, PdfPanelKeyModifiers modifiers)
    {
        PdfPanelKeyEventArgs args = new(key, modifiers);
        KeyPressed?.Invoke(this, args);
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
        float deltaX = position.ViewportPosition.X - pressPosition.ViewportPosition.X;
        float deltaY = position.ViewportPosition.Y - pressPosition.ViewportPosition.Y;

        return (deltaX * deltaX) + (deltaY * deltaY)
            >= _parameters.MinimumDragDistance * _parameters.MinimumDragDistance;
    }
}

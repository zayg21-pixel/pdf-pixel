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
    /// Reports a pointer button press at the given position.
    /// </summary>
    public void Press(in PdfPanelPointerPosition position)
    {
        _pressPosition = position;
        _lastPosition = position;
        IsDragging = false;

        PdfPanelPointerEventArgs args = new(position);
        PointerPressed?.Invoke(this, args);
    }

    /// <summary>
    /// Reports a pointer move to the given position.
    /// </summary>
    public void Move(in PdfPanelPointerPosition position)
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

    /// <summary>
    /// Reports a pointer button release at the given position.
    /// </summary>
    public void Release(in PdfPanelPointerPosition position)
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

    /// <summary>
    /// Ends a press or drag in progress without raising <see cref="PointerReleased"/> or <see cref="PointerClicked"/>.
    /// </summary>
    public void Cancel()
    {
        PdfPanelPointerPosition? pressPosition = _pressPosition;
        bool wasDragging = IsDragging;

        _pressPosition = null;
        IsDragging = false;

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

    private bool HasTravelledDragDistance(in PdfPanelPointerPosition pressPosition, in PdfPanelPointerPosition position)
    {
        float deltaX = position.ViewportPosition.X - pressPosition.ViewportPosition.X;
        float deltaY = position.ViewportPosition.Y - pressPosition.ViewportPosition.Y;

        return (deltaX * deltaX) + (deltaY * deltaY)
            >= _parameters.MinimumDragDistance * _parameters.MinimumDragDistance;
    }
}

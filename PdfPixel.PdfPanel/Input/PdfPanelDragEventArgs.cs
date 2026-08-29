namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Provides pointer data for the drag events of <see cref="PdfPanelInputProcessor"/>.
/// </summary>
public sealed class PdfPanelDragEventArgs : PdfPanelPointerEventArgs
{
    /// <summary>
    /// Initializes the event args with the position the drag started at and the current pointer position.
    /// </summary>
    public PdfPanelDragEventArgs(in PdfPanelPointerPosition startPosition, in PdfPanelPointerPosition position)
        : base(position)
    {
        StartPosition = startPosition;
    }

    /// <summary>
    /// Position the drag started at.
    /// </summary>
    public PdfPanelPointerPosition StartPosition { get; }
}

using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Provides pointer data for the pointer events of <see cref="PdfPanelInputProcessor"/>.
/// </summary>
public class PdfPanelPointerEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the event args with the pointer position.
    /// </summary>
    public PdfPanelPointerEventArgs(in PdfPanelPointerPosition position) => Position = position;

    /// <summary>
    /// Pointer position.
    /// </summary>
    public PdfPanelPointerPosition Position { get; }

    /// <summary>
    /// Whether a subscriber has handled the event.
    /// </summary>
    public bool IsHandled { get; set; }

    /// <summary>
    /// Cursor shape a subscriber requests for <see cref="Position"/>.
    /// </summary>
    public PdfPanelCursor Cursor { get; set; }
}

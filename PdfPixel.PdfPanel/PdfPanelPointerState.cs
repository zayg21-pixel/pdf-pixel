namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents the interaction state of the pointer in the PDF panel.
/// </summary>
public enum PdfPanelPointerState
{
    /// <summary>
    /// No UI element is active.
    /// </summary>
    None,

    /// <summary>
    /// Pointer is hovering over UI element.
    /// </summary>
    Hovered,

    /// <summary>
    /// Pointer button is pressed on UI element.
    /// </summary>
    Pressed
}

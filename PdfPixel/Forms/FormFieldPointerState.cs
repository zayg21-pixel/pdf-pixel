namespace PdfPixel.Forms;

/// <summary>
/// Represents the pointer state for form field interactions.
/// </summary>
public enum FormFieldPointerState
{
    /// <summary>
    /// Default state, pointer is over the field but not pressed.
    /// </summary>
    Hover,

    /// <summary>
    /// Pointer button is pressed down on the field.
    /// </summary>
    Pressed,

    /// <summary>
    /// Pointer is pressed and being dragged (for selection or scrolling).
    /// </summary>
    Dragging
}

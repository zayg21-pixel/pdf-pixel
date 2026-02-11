using SkiaSharp;

namespace PdfPixel.Forms;

/// <summary>
/// Defines mouse interaction behavior for PDF form fields.
/// </summary>
public interface IFormFieldMouseInteraction
{
    /// <summary>
    /// Handles mouse down event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled and should not propagate further.</returns>
    bool OnMouseDown(SKPoint position, FormFieldPointerState state);

    /// <summary>
    /// Handles mouse up event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled and should not propagate further.</returns>
    bool OnMouseUp(SKPoint position, FormFieldPointerState state);

    /// <summary>
    /// Handles mouse move event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled and should not propagate further.</returns>
    bool OnMouseMove(SKPoint position, FormFieldPointerState state);

    /// <summary>
    /// Handles mouse enter event when the pointer enters the field's bounds.
    /// </summary>
    void OnMouseEnter();

    /// <summary>
    /// Handles mouse leave event when the pointer exits the field's bounds.
    /// </summary>
    void OnMouseLeave();
}

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

namespace PdfPixel.Forms;

/// <summary>
/// Defines keyboard interaction behavior for PDF form fields.
/// </summary>
public interface IFormFieldKeyboardInteraction
{
    /// <summary>
    /// Handles key down event on the form field.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">Keyboard modifiers (Ctrl, Shift, Alt).</param>
    /// <returns>True if the event was handled and should not propagate further.</returns>
    bool OnKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers);

    /// <summary>
    /// Handles text input event on the form field.
    /// </summary>
    /// <param name="text">The text that was input.</param>
    /// <returns>True if the event was handled and should not propagate further.</returns>
    bool OnTextInput(string text);

    /// <summary>
    /// Gets a value indicating whether this field can receive keyboard focus.
    /// </summary>
    bool CanReceiveFocus { get; }

    /// <summary>
    /// Gets a value indicating whether this field currently has keyboard focus.
    /// </summary>
    bool HasFocus { get; }

    /// <summary>
    /// Sets focus to this field.
    /// </summary>
    void Focus();

    /// <summary>
    /// Removes focus from this field.
    /// </summary>
    void Blur();
}

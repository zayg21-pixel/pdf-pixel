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

/// <summary>
/// Represents keyboard keys for form field interactions.
/// </summary>
public enum FormFieldKey
{
    Unknown,
    Enter,
    Tab,
    Escape,
    Backspace,
    Delete,
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    PageUp,
    PageDown,
    Space
}

/// <summary>
/// Keyboard modifier flags for form field interactions.
/// </summary>
[System.Flags]
public enum FormFieldKeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2
}

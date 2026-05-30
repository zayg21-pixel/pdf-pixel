namespace PdfPixel.Forms;

/// <summary>
/// Keyboard modifier flags for form field interactions.
/// </summary>
[System.Flags]
public enum FormFieldKeyModifiers
{
    /// <summary>
    /// No modifier keys held.
    /// </summary>
    None = 0,

    /// <summary>
    /// Shift key held.
    /// </summary>
    Shift = 1 << 0,

    /// <summary>
    /// Control key held.
    /// </summary>
    Control = 1 << 1,

    /// <summary>
    /// Alt key held.
    /// </summary>
    Alt = 1 << 2
}

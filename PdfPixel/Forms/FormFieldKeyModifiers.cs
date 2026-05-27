namespace PdfPixel.Forms;

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

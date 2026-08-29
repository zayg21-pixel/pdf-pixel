using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Keyboard modifier flags held while a key is pressed.
/// </summary>
[Flags]
public enum PdfPanelKeyModifiers
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

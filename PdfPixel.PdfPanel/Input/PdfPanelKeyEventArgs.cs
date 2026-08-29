using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Provides key data for <see cref="PdfPanelInputProcessor.KeyPressed"/>.
/// </summary>
public sealed class PdfPanelKeyEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the event args with the key and the modifiers held while it was pressed.
    /// </summary>
    public PdfPanelKeyEventArgs(PdfPanelKey key, PdfPanelKeyModifiers modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Key that was pressed.
    /// </summary>
    public PdfPanelKey Key { get; }

    /// <summary>
    /// Modifier keys held while <see cref="Key"/> was pressed.
    /// </summary>
    public PdfPanelKeyModifiers Modifiers { get; }

    /// <summary>
    /// Whether a subscriber has handled the event.
    /// </summary>
    public bool IsHandled { get; set; }
}

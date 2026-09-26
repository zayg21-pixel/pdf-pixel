namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Pointer interaction thresholds of a PDF panel.
/// </summary>
public sealed class PdfPanelInteractionSettings
{
    /// <summary>
    /// Distance the pointer travels from the press position before a press becomes a drag, in viewport pixels.
    /// </summary>
    public float MinimumDragDistance { get; set; } = 4f;

    /// <summary>
    /// Distance from a character within which the pointer counts as being over it, in unscaled page space.
    /// </summary>
    public float CharacterHitRadius { get; set; } = 10f;
}

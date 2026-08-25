using PdfPixel.Color;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Configuration values a <see cref="PdfPanelTextSelector"/> is created with.
/// </summary>
public sealed class PdfPanelTextSelectorParameters
{
    /// <summary>
    /// Distance the pointer travels before a press becomes a drag selection, in unscaled page space.
    /// </summary>
    public float MinimumDragDistance { get; set; } = 4f;

    /// <summary>
    /// Distance from a character within which the pointer counts as being over it, in unscaled page space.
    /// </summary>
    public float CharacterHitRadius { get; set; } = 10f;

    /// <summary>
    /// Vertical distance between two characters, as a fraction of character height, within which
    /// they highlight as one strip.
    /// </summary>
    public float LineMergeThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Color the selected text is highlighted with.
    /// </summary>
    public PdfColor HighlightColor { get; set; } = new(50f / 255f, 100f / 255f, 220f / 255f, 80f / 255f);
}

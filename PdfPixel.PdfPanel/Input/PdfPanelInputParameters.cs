namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// Configuration values a <see cref="PdfPanelInputProcessor"/> is created with.
/// </summary>
public sealed class PdfPanelInputParameters
{
    /// <summary>
    /// Distance the pointer travels from the press position before a press becomes a drag, in viewport pixels.
    /// </summary>
    public float MinimumDragDistance { get; set; } = 4f;
}

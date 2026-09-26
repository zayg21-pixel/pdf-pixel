namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Scale limits and zoom step of a PDF panel.
/// </summary>
public sealed class PdfPanelZoomSettings
{
    /// <summary>
    /// Minimum allowed zoom scale factor.
    /// </summary>
    public float MinScale { get; set; } = 0.1f;

    /// <summary>
    /// Maximum allowed zoom scale factor.
    /// </summary>
    public float MaxScale { get; set; } = 10.0f;

    /// <summary>
    /// Proportional scale change of a single zoom step (e.g. 0.1 for 10%).
    /// </summary>
    public float ZoomStep { get; set; } = 0.1f;
}

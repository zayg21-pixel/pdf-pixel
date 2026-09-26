using PdfPixel.Color;
using System;

namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Colors and decorations of a PDF panel.
/// </summary>
public sealed class PdfPanelAppearanceSettings : IEquatable<PdfPanelAppearanceSettings>
{
    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfColor BackgroundColor { get; set; } = PdfColors.LightGray;

    /// <summary>
    /// Corner radius for page rendering in unscaled page space.
    /// A value of 0 renders pages with sharp corners.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Draws an animated placeholder over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation { get; set; } = true;

    /// <summary>
    /// Color the selected text is highlighted with.
    /// </summary>
    public PdfColor SelectionColor { get; set; } = new(50f / 255f, 100f / 255f, 220f / 255f, 80f / 255f);

    /// <summary>
    /// Color the search matches are highlighted with.
    /// </summary>
    public PdfColor SearchMatchColor { get; set; } = new(255f / 255f, 200f / 255f, 0f / 255f, 100f / 255f);

    /// <summary>
    /// Vertical distance between two characters, as a fraction of character height, within which
    /// they highlight as one strip.
    /// </summary>
    public float LineMergeThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Returns a copy of these settings.
    /// </summary>
    public PdfPanelAppearanceSettings Clone() => (PdfPanelAppearanceSettings)MemberwiseClone();

    /// <inheritdoc />
    public bool Equals(PdfPanelAppearanceSettings? other)
    {
        if (other == null)
        {
            return false;
        }

        return BackgroundColor.Equals(other.BackgroundColor)
            && PageCornerRadius == other.PageCornerRadius
            && ShowPageLoadingAnimation == other.ShowPageLoadingAnimation
            && SelectionColor.Equals(other.SelectionColor)
            && SearchMatchColor.Equals(other.SearchMatchColor)
            && LineMergeThreshold == other.LineMergeThreshold;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfPanelAppearanceSettings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(BackgroundColor, PageCornerRadius, ShowPageLoadingAnimation, SelectionColor, SearchMatchColor, LineMergeThreshold);
}

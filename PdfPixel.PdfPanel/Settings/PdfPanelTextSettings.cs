using System;

namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Text extraction and text highlight options of a PDF panel.
/// </summary>
public sealed class PdfPanelTextSettings : IEquatable<PdfPanelTextSettings>
{
    /// <summary>
    /// Whether the words of every page are extracted, not only of the pages that are rendered.
    /// </summary>
    public bool ExtractText { get; set; }

    /// <summary>
    /// Vertical distance between two characters, as a fraction of character height, within which
    /// they highlight as one strip.
    /// </summary>
    public float LineMergeThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Returns a copy of these settings.
    /// </summary>
    public PdfPanelTextSettings Clone() => (PdfPanelTextSettings)MemberwiseClone();

    /// <inheritdoc />
    public bool Equals(PdfPanelTextSettings? other)
    {
        if (other == null)
        {
            return false;
        }

        return ExtractText == other.ExtractText
            && LineMergeThreshold == other.LineMergeThreshold;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfPanelTextSettings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ExtractText, LineMergeThreshold);
}

using System;

namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Options that control how the text search query is matched.
/// </summary>
public sealed class PdfPanelSearchSettings : IEquatable<PdfPanelSearchSettings>
{
    /// <summary>
    /// Whether letter case must match.
    /// </summary>
    public bool MatchCase { get; set; }

    /// <summary>
    /// Whether a match must start and end on word boundaries.
    /// </summary>
    public bool WholeWord { get; set; }

    /// <summary>
    /// Returns a copy of these settings.
    /// </summary>
    public PdfPanelSearchSettings Clone() => (PdfPanelSearchSettings)MemberwiseClone();

    /// <inheritdoc />
    public bool Equals(PdfPanelSearchSettings? other)
    {
        if (other == null)
        {
            return false;
        }

        return MatchCase == other.MatchCase
            && WholeWord == other.WholeWord;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfPanelSearchSettings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(MatchCase, WholeWord);
}

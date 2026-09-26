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
    /// Whether diacritics must match, so that "e" does not match "é".
    /// </summary>
    public bool MatchDiacritics { get; set; }

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
            && WholeWord == other.WholeWord
            && MatchDiacritics == other.MatchDiacritics;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfPanelSearchSettings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(MatchCase, WholeWord, MatchDiacritics);
}

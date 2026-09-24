namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Options that control how the text search query is matched.
/// </summary>
public sealed class PdfPanelSearchOptions
{
    /// <summary>
    /// Whether letter case must match.
    /// </summary>
    public bool MatchCase { get; set; }

    /// <summary>
    /// Whether a match must start and end on word boundaries.
    /// </summary>
    public bool WholeWord { get; set; }
}

namespace PdfPixel.Text;

/// <summary>
/// Properties controlling how <see cref="PdfTextExtractor"/> extracts words from a content tree.
/// </summary>
public sealed class PdfTextExtractionProperties
{
    /// <summary>
    /// When true, text inside artifacts (Artifact tag) is extracted as well.
    /// </summary>
    public bool IncludeArtifacts { get; set; }

    /// <summary>
    /// When true, the text of marked content with replacement text (/ActualText) is replaced by that text.
    /// </summary>
    public bool UseActualText { get; set; } = true;

    /// <summary>
    /// Horizontal gap between two characters on one line, as a fraction of character height, above which a space is generated.
    /// </summary>
    public float WordGapRatio { get; set; } = 0.1f;

    /// <summary>
    /// Vertical distance between two character centers, as a fraction of character height, above which a line break is generated.
    /// </summary>
    public float LineOffsetRatio { get; set; } = 0.5f;
}

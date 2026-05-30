namespace PdfPixel.PdfPanel;

/// <summary>
/// Information about a page in a PDF document.
/// </summary>
public readonly struct PdfPanelPageInfo
{
    /// <summary>
    /// Initializes page info with the given label, dimensions, and PDF rotation.
    /// </summary>
    public PdfPanelPageInfo(string label, float width, float height, int rotation)
    {
        Label = label;
        Width = width;
        Height = height;
        Rotation = rotation;
    }

    /// <summary>
    /// Human-readable page label (e.g. "iv", "1", "A-1") as defined in the PDF.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Original page width without rotation.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Original page height without rotation.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Page rotation in degrees.
    /// </summary>
    public int Rotation { get; }
}

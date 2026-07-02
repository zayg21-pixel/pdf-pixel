namespace PdfPixel.PdfPanel;

/// <summary>
/// Information about a page in a PDF document.
/// </summary>
public readonly struct PdfPanelPageInfo
{
    /// <summary>
    /// Initializes page info with the given label, dimensions, crop box origin, and PDF rotation.
    /// </summary>
    public PdfPanelPageInfo(string label, float width, float height, float left, float top, int rotation)
    {
        Label = label;
        Width = width;
        Height = height;
        Left = left;
        Top = top;
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
    /// X coordinate of the crop box origin in PDF coordinates (bottom-left origin, Y-up).
    /// </summary>
    public float Left { get; }

    /// <summary>
    /// Y coordinate of the crop box origin in PDF coordinates (bottom-left origin, Y-up).
    /// </summary>
    public float Top { get; }

    /// <summary>
    /// Page rotation in degrees.
    /// </summary>
    public int Rotation { get; }
}

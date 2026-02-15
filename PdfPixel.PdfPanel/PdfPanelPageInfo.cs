namespace PdfPixel.PdfPanel;

/// <summary>
/// Information about a page in a PDF document.
/// </summary>
public readonly struct PdfPanelPageInfo
{
    public PdfPanelPageInfo(string label, float width, float height, int rotation)
    {
        Label = label;
        Width = width;
        Height = height;
        Rotation = rotation;
    }

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

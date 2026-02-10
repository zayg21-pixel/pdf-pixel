using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Information about a page in a PDF document.
/// </summary>
public readonly struct PdfPanelPageInfo
{
    public PdfPanelPageInfo(float width, float height, int rotation)
    {
        Width = width;
        Height = height;
        Rotation = rotation;
    }

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

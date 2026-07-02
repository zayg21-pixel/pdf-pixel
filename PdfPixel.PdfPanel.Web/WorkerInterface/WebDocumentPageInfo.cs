namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// Information about web document page.
/// </summary>
public class WebDocumentPageInfo
{
    public string Label { get; set; }

    /// <summary>
    /// Original page width without rotation.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Original page height without rotation.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// X coordinate of the crop box origin in PDF coordinates (bottom-left origin, Y-up).
    /// </summary>
    public float Left { get; set; }

    /// <summary>
    /// Y coordinate of the crop box origin in PDF coordinates (bottom-left origin, Y-up).
    /// </summary>
    public float Top { get; set; }

    /// <summary>
    /// Page rotation in degrees.
    /// </summary>
    public int Rotation { get; set; }

    public PdfPanelPageInfo ToPdfPanelPageInfo()
    {
        return new PdfPanelPageInfo(Label, Width, Height, Left, Top, Rotation);
    }

    public static WebDocumentPageInfo FromPdfPanelPageInfo(PdfPanelPageInfo pageInfo)
    {
        return new WebDocumentPageInfo
        {
            Label = pageInfo.Label,
            Width = pageInfo.Width,
            Height = pageInfo.Height,
            Left = pageInfo.Left,
            Top = pageInfo.Top,
            Rotation = pageInfo.Rotation
        };
    }
}

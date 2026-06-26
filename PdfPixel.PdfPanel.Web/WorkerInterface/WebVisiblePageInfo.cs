namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// JSON-serializable representation of <see cref="VisiblePageInfo"/>.
/// </summary>
public class WebVisiblePageInfo
{
    public int PageNumber { get; set; }

    public float OffsetX { get; set; }

    public float OffsetY { get; set; }

    public int UserRotation { get; set; }

    public WebDocumentPageInfo PageInfo { get; set; }

    public VisiblePageInfo ToVisiblePageInfo()
    {
        return new VisiblePageInfo(
            PageNumber,
            new SkiaSharp.SKPoint(OffsetX, OffsetY),
            PageInfo.ToPdfPanelPageInfo(),
            UserRotation);
    }

    public static WebVisiblePageInfo FromVisiblePageInfo(VisiblePageInfo visiblePageInfo)
    {
        return new WebVisiblePageInfo
        {
            PageNumber = visiblePageInfo.PageNumber,
            OffsetX = visiblePageInfo.Offset.X,
            OffsetY = visiblePageInfo.Offset.Y,
            UserRotation = visiblePageInfo.UserRotation,
            PageInfo = WebDocumentPageInfo.FromPdfPanelPageInfo(visiblePageInfo.Info)
        };
    }
}

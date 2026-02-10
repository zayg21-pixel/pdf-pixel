using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;

namespace PdfPixel.PdfPanel;

internal readonly struct VisiblePageInfo
{
    public VisiblePageInfo(int pageNumber, SKPoint offset, PdfPanelPageInfo pageInfo, int userRotation)
    {
        PageNumber = pageNumber;
        Offset = offset;
        Info = pageInfo;

        var normalizedUserRotation = userRotation % 360;

        if (normalizedUserRotation < 0)
        {
            normalizedUserRotation += 360;
        }

        UserRotation = normalizedUserRotation;
    }

    /// <summary>
    /// Gets the page number.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the offset of the page.
    /// Page offset is the unscaled distance from the top-left corner of the page to the top-left corner of the document.
    /// </summary>
    public SKPoint Offset { get; }

    /// <summary>
    /// Gets the page information.
    /// </summary>
    public PdfPanelPageInfo Info { get; }

    /// <summary>
    /// Gets the user rotation of the page.
    /// </summary>
    public int UserRotation { get; }

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public SKSize RotatedSize => Info.GetRotatedSize(UserRotation);

    /// <summary>
    /// Gets the scaled bounds of the page.
    /// </summary>
    /// <param name="scale">Scale factor.</param>
    /// <returns></returns>
    internal SKRect GetScaledBounds(double scale) => SKRect.Create((float)(Offset.X * scale), (float)(Offset.Y * scale), (float)(RotatedSize.Width * scale), (float)(RotatedSize.Height * scale));
}

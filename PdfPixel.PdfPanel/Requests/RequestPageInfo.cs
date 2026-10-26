using PdfPixel.Commands.Processing;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Rendering;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Immutable snapshot of a page's rendering parameters captured at the start of each render pass.
/// </summary>
internal readonly struct RequestPageInfo
{
    /// <summary>
    /// Initialises a new snapshot for a page of a request.
    /// </summary>
    public RequestPageInfo(
        PdfPanelPage page,
        in PdfPoint offset,
        in PdfSize panelSize,
        float scale,
        int tileSize)
    {
        Page = page;
        Offset = offset;
        Rotation = page.Rotation;

        PdfRectangle panelRect = PdfRectangle.FromLocationAndSize(0, 0, panelSize.Width, panelSize.Height);
        PdfRectangle visibleContent = GetContentToPanelMatrix(scale).Invert().MapRect(panelRect);
        PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, page.Size.Width, page.Size.Height);

        VisibleRegion = (PdfRectangle.IntersectsWith(visibleContent, pageBounds))
            ? PdfRectangle.Intersect(PdfPageContentTiler.SnapToTileGrid(visibleContent, scale, tileSize), pageBounds)
            : PdfRectangle.Empty;

        if (pageBounds.Width * scale <= panelSize.Width && pageBounds.Height * scale <= panelSize.Height)
        {
            RegionOfInterest = null;
        }
        else
        {
            RegionOfInterest = VisibleRegion;
        }
    }

    /// <summary>
    /// Gets the page number.
    /// </summary>
    public int PageNumber => Page.PageNumber;

    /// <summary>
    /// Gets the offset of the page.
    /// Page offset is the unscaled distance from the top-left corner of the page to the top-left corner of the document.
    /// </summary>
    public PdfPoint Offset { get; }

    /// <summary>
    /// Gets the page.
    /// </summary>
    public PdfPanelPage Page { get; }

    /// <summary>
    /// Gets the rotation of the page.
    /// </summary>
    public int Rotation { get; }

    /// <summary>
    /// Gets the part of the page covered by the canvas, in content coordinates, expanded to the tile grid and clamped
    /// to the page bounds, or <see cref="PdfRectangle.Empty"/> when the canvas does not cover the page.
    /// </summary>
    public PdfRectangle VisibleRegion { get; }

    /// <summary>
    /// Gets the part of the page its content is decoded for: <see langword="null"/> for the whole page when the page
    /// fits within the canvas at the current scale, otherwise <see cref="VisibleRegion"/>.
    /// </summary>
    public PdfRectangle? RegionOfInterest { get; }

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public PdfSize RotatedSize => Page.GetRotatedSize(Rotation);

    /// <summary>
    /// Matrix that maps this page's content coordinates (top-left origin, Y-down, unrotated,
    /// matching <see cref="Page"/> dimensions — the space recorded pictures and command
    /// replay operate in) directly to panel pixels. Combines page rotation with the
    /// scroll-offset translation and render scale applied to the panel before content is
    /// drawn. Invert it to map panel pixels back to content coordinates, e.g. to derive a
    /// region of interest from the visible panel area.
    /// </summary>
    public PdfMatrix GetContentToPanelMatrix(float scale)
    {
        PdfSize rotatedSize = RotatedSize;

        float rotationOffsetX = Rotation switch { 90 or 180 => rotatedSize.Width, _ => 0f };
        float rotationOffsetY = Rotation switch { 180 or 270 => rotatedSize.Height, _ => 0f };

        return PdfMatrix.CreateRotationDegrees(Rotation)
            .PostConcat(PdfMatrix.CreateScale(scale, scale))
            .PostConcat(PdfMatrix.CreateTranslation(
                PdfCommandProcessingUtilities.SnapToWholePixel((Offset.X + rotationOffsetX) * scale),
                PdfCommandProcessingUtilities.SnapToWholePixel((Offset.Y + rotationOffsetY) * scale)));
    }

    /// <summary>
    /// Converts a rectangle from PDF coordinates (bottom-left origin, Y-up) to page coordinates (top-left origin, Y-down).
    /// </summary>
    /// <param name="pdfRect">Rectangle in PDF coordinates.</param>
    /// <returns>Rectangle in page coordinates.</returns>
    public PdfRectangle FromPdfRect(in PdfRectangle pdfRect)
    {
        PdfRectangle cropBox = Page.PdfPage.CropBox;
        float userUnit = Page.PdfPage.UserUnit;

        return PdfRectangle.FromLocationAndSize(
            (pdfRect.Left - cropBox.Left) * userUnit,
            (cropBox.Height + cropBox.Top - pdfRect.Bottom) * userUnit,
            pdfRect.Width * userUnit,
            pdfRect.Height * userUnit);
    }

    /// <summary>
    /// Returns whether content decoded for <paramref name="decodedRegion"/> covers <paramref name="requestedRegion"/>,
    /// where <see langword="null"/> stands for the whole page and an empty region needs no content.
    /// </summary>
    public static bool CoversRegion(PdfRectangle? decodedRegion, PdfRectangle? requestedRegion)
    {
        if (decodedRegion == null)
        {
            return true;
        }

        if (requestedRegion == null)
        {
            return false;
        }

        return requestedRegion.Value.IsEmpty || decodedRegion.Value.Contains(requestedRegion.Value);
    }
}

using PdfPixel.Commands.Processing;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Rendering;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Immutable snapshot of a page's rendering parameters captured at the start of each render pass.
/// </summary>
public readonly struct VisiblePageInfo
{
    /// <summary>
    /// Initialises a new snapshot for a visible page.
    /// </summary>
    public VisiblePageInfo(
        int pageNumber,
        in PdfPoint offset,
        in PdfPanelPageInfo pageInfo,
        int userRotation,
        in PdfSize canvasSize,
        float scale,
        int tileSize)
    {
        PageNumber = pageNumber;
        Offset = offset;
        Info = pageInfo;

        int normalizedUserRotation = userRotation % 360;

        if (normalizedUserRotation < 0)
        {
            normalizedUserRotation += 360;
        }

        UserRotation = normalizedUserRotation;

        PdfRectangle canvasRect = PdfRectangle.FromLocationAndSize(0, 0, canvasSize.Width, canvasSize.Height);
        PdfRectangle visibleContent = GetContentToCanvasMatrix(scale).Invert().MapRect(canvasRect);
        PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, pageInfo.Width, pageInfo.Height);

        RegionOfInterest = PdfRectangle.Intersect(
            PdfPageContentTiler.SnapToTileGrid(visibleContent, scale, tileSize),
            pageBounds);
    }

    /// <summary>
    /// Gets the page number.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the offset of the page.
    /// Page offset is the unscaled distance from the top-left corner of the page to the top-left corner of the document.
    /// </summary>
    public PdfPoint Offset { get; }

    /// <summary>
    /// Gets the page information.
    /// </summary>
    public PdfPanelPageInfo Info { get; }

    /// <summary>
    /// Gets the user rotation of the page.
    /// </summary>
    public int UserRotation { get; }

    /// <summary>
    /// Gets the part of the page covered by the canvas, in content coordinates,
    /// expanded to the tile grid and clamped to the page bounds.
    /// </summary>
    public PdfRectangle RegionOfInterest { get; }

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public PdfSize RotatedSize => Info.GetRotatedSize(UserRotation);

    /// <summary>
    /// Matrix that maps this page's content coordinates (top-left origin, Y-down, unrotated,
    /// matching <see cref="Info"/> dimensions — the space recorded pictures and command
    /// replay operate in) directly to canvas pixels. Combines page rotation with the
    /// scroll-offset translation and render scale applied to the canvas before content is
    /// drawn. Invert it to map canvas pixels back to content coordinates, e.g. to derive a
    /// region of interest from the visible canvas area.
    /// </summary>
    public PdfMatrix GetContentToCanvasMatrix(float scale)
    {
        int rotation = Info.GetTotalRotation(UserRotation);
        PdfSize rotatedSize = RotatedSize;

        float rotationOffsetX = rotation switch { 90 or 180 => rotatedSize.Width, _ => 0f };
        float rotationOffsetY = rotation switch { 180 or 270 => rotatedSize.Height, _ => 0f };

        return PdfMatrix.CreateRotationDegrees(rotation)
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
        return PdfRectangle.FromLocationAndSize(
            pdfRect.Left - Info.Left,
            Info.Height + Info.Top - pdfRect.Bottom,
            pdfRect.Width,
            pdfRect.Height);
    }
}

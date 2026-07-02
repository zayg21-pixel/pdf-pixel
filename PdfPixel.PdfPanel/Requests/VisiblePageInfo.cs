using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Immutable snapshot of a page's rendering parameters captured at the start of each render pass.
/// </summary>
public readonly struct VisiblePageInfo
{
    /// <summary>
    /// Initialises a new snapshot for a visible page.
    /// </summary>
    public VisiblePageInfo(int pageNumber, SKPoint offset, in PdfPanelPageInfo pageInfo, int userRotation)
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
    /// Rotation matrix that maps unrotated page content coordinates to the rotated drawing space.
    /// Used when drawing SKPictures onto the canvas after Scale and Translate have been applied.
    /// Returns identity when the page has no rotation.
    /// </summary>
    public SKMatrix ContentTransform
    {
        get
        {
            int rotation = Info.GetTotalRotation(UserRotation);
            if (rotation == 0)
            {
                return SKMatrix.Identity;
            }

            float tx = rotation switch { 180 or 270 => -Info.Width, _ => 0f };
            float ty = rotation switch { 90 or 180 => -Info.Height, _ => 0f };

            return SKMatrix.Concat(
                SKMatrix.CreateRotationDegrees(rotation),
                SKMatrix.CreateTranslation(tx, ty));
        }
    }

    /// <summary>
    /// Matrix that maps this page's content coordinates (top-left origin, Y-down, unrotated,
    /// matching <see cref="Info"/> dimensions — the space recorded pictures and command
    /// replay operate in) directly to canvas pixels. Combines <see cref="ContentTransform"/>
    /// with the same scroll-offset translation and render scale applied to the canvas before
    /// content is drawn. Invert it to map canvas pixels back to content coordinates, e.g. to
    /// derive a region of interest from the visible canvas area.
    /// </summary>
    public SKMatrix GetContentToCanvasMatrix(float scale)
    {
        return ContentTransform
            .PostConcat(SKMatrix.CreateTranslation(Offset.X, Offset.Y))
            .PostConcat(SKMatrix.CreateScale(scale, scale));
    }

    /// <summary>
    /// Converts a rectangle from PDF coordinates (bottom-left origin, Y-up) to page coordinates (top-left origin, Y-down).
    /// </summary>
    /// <param name="pdfRect">Rectangle in PDF coordinates.</param>
    /// <returns>Rectangle in page coordinates.</returns>
    public SKRect FromPdfRect(SKRect pdfRect)
    {
        return SKRect.Create(
            pdfRect.Left - Info.Left,
            Info.Height + Info.Top - pdfRect.Bottom,
            pdfRect.Width,
            pdfRect.Height);
    }
}

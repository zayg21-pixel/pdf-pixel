using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;

namespace PdfPixel.PdfPanel;

public readonly struct VisiblePageInfo
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
    /// <returns>Bounds scaled to factor.</returns>
    public SKRect GetScaledBounds(float scale) => SKRect.Create(Offset.X * scale, Offset.Y * scale, RotatedSize.Width * scale, RotatedSize.Height * scale);

    /// <summary>
    /// Gets the transformation matrix from viewport coordinates to page coordinates for this visible page.
    /// </summary>
    /// <param name="scale">Scale factor.</param>
    /// <returns>Matrix that transforms viewport coordinates to page coordinates.</returns>
    public SKMatrix GetToPageMatrix(double scale)
    {
        var matrix = SKMatrix.Identity;

        // Apply rotation (about page origin)
        int totalRotation = Info.GetTotalRotation(UserRotation);
        if (totalRotation != 0)
        {
            matrix = matrix.PostConcat(PdfPanelPageExtensions.GetInverseRotationMatrix(RotatedSize.Width, RotatedSize.Height, -totalRotation));
        }

        // Apply translation by unscaled offset
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(Offset.X, Offset.Y));


        // Apply scale
        matrix = matrix.PostConcat(SKMatrix.CreateScale((float)scale, (float)scale));

        return matrix;
    }
}

using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Visible page of a <see cref="PdfPanelFrame"/>.
/// </summary>
public readonly struct PdfPanelFramePage
{
    private readonly PdfMatrix _contentToPanel;

    internal PdfPanelFramePage(in VisiblePageInfo page, float scale)
    {
        PageNumber = page.PageNumber;
        Info = page.Info;
        UserRotation = page.UserRotation;
        _contentToPanel = page.GetContentToPanelMatrix(scale);
    }

    /// <summary>
    /// Gets the page number.
    /// </summary>
    public int PageNumber { get; }

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
    public PdfSize RotatedSize => Info.GetRotatedSize(UserRotation);

    /// <summary>
    /// Returns the matrix that maps panel pixels to page space rotated by <paramref name="rotation"/> degrees,
    /// with the origin at the top-left corner of the rotated page.
    /// </summary>
    /// <param name="rotation">Rotation of the page space relative to the unrotated page content, a multiple of 90.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rotation"/> is not a multiple of 90.</exception>
    public PdfMatrix GetPanelToPage(int rotation)
    {
        int normalizedRotation = rotation % 360;

        if (normalizedRotation < 0)
        {
            normalizedRotation += 360;
        }

        if (normalizedRotation % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rotation));
        }

        float width = Info.CropBox.Width;
        float height = Info.CropBox.Height;
        float rotatedWidth = (normalizedRotation % 180 == 0) ? width : height;
        float rotatedHeight = (normalizedRotation % 180 == 0) ? height : width;
        float rotationOffsetX = normalizedRotation switch { 90 or 180 => rotatedWidth, _ => 0f };
        float rotationOffsetY = normalizedRotation switch { 180 or 270 => rotatedHeight, _ => 0f };

        PdfMatrix contentToPage = PdfMatrix.CreateRotationDegrees(normalizedRotation)
            .PostConcat(PdfMatrix.CreateTranslation(rotationOffsetX, rotationOffsetY));

        return _contentToPanel.Invert().PostConcat(contentToPage);
    }
}

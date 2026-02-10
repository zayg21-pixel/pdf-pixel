using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Extension methods for <see cref="PdfPanelPage"/> and <see cref="PdfPanelPageInfo"/>.
/// </summary>
public static class PdfPanelPageExtensions
{
    /// <summary>
    /// Gets the total rotation in degrees after applying user rotation.
    /// </summary>
    /// <param name="pageInfo">The page information.</param>
    /// <param name="userRotation">User-applied rotation in degrees.</param>
    /// <returns>Total rotation normalized to 0-359 degrees.</returns>
    public static int GetTotalRotation(this PdfPanelPageInfo pageInfo, int userRotation)
    {
        int totalRotation = pageInfo.Rotation + userRotation;
        totalRotation = totalRotation % 360;

        if (totalRotation < 0)
        {
            totalRotation += 360;
        }

        return totalRotation;
    }

    /// <summary>
    /// Gets the size of the page after applying rotation.
    /// </summary>
    /// <param name="pageInfo">The page information.</param>
    /// <param name="userRotation">User-applied rotation in degrees.</param>
    /// <returns>The rotated page size with width and height swapped if rotated 90 or 270 degrees.</returns>
    public static SKSize GetRotatedSize(this PdfPanelPageInfo pageInfo, int userRotation)
    {
        int totalRotation = pageInfo.GetTotalRotation(userRotation);

        if (totalRotation % 180 != 0)
        {
            return new SKSize(pageInfo.Height, pageInfo.Width);
        }

        return new SKSize(pageInfo.Width, pageInfo.Height);
    }

    /// <summary>
    /// Gets the total rotation in degrees for the page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>Total rotation normalized to 0-359 degrees.</returns>
    public static int GetTotalRotation(this PdfPanelPage page)
    {
        return page.Info.GetTotalRotation(page.UserRotation);
    }

    /// <summary>
    /// Gets the size of the page after applying rotation.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The rotated page size.</returns>
    public static SKSize GetRotatedSize(this PdfPanelPage page)
    {
        return page.Info.GetRotatedSize(page.UserRotation);
    }

    /// <summary>
    /// Gets the size of the page after applying rotation and scale.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="scale">Scale factor.</param>
    /// <returns>The rotated and scaled page size.</returns>
    public static SKSize GetRotatedScaledSize(this PdfPanelPage page, float scale)
    {
        if (page == null)
        {
            throw new System.ArgumentNullException(nameof(page));
        }

        SKSize rotatedSize = page.GetRotatedSize();
        return new SKSize(rotatedSize.Width * scale, rotatedSize.Height * scale);
    }

    /// <summary>
    /// Gets the bounding rectangle of the page in unscaled coordinate space.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The page bounds.</returns>
    public static SKRect GetBounds(this PdfPanelPage page)
    {
        SKSize rotatedSize = page.GetRotatedSize();
        return SKRect.Create(page.Offset, rotatedSize);
    }

    /// <summary>
    /// Gets the bounding rectangle of the page with scaled offset and size.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="scale">Scale factor.</param>
    /// <returns>The scaled page bounds.</returns>
    public static SKRect GetScaledBounds(this PdfPanelPage page, float scale)
    {
        SKSize rotatedSize = page.GetRotatedSize();
        return SKRect.Create(page.Offset.X * scale, page.Offset.Y * scale, rotatedSize.Width * scale, rotatedSize.Height * scale);
    }

    /// <summary>
    /// Gets the bounding rectangle of the page in scaled coordinate space with unscaled offset.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="scale">Scale factor.</param>
    /// <returns>The page bounds with scaled dimensions.</returns>
    public static SKRect GetScaledPageBounds(this PdfPanelPage page, float scale)
    {
        SKSize rotatedScaledSize = page.GetRotatedScaledSize(scale);
        return SKRect.Create(page.Offset, rotatedScaledSize);
    }

    /// <summary>
    /// Determines whether the page is visible within the specified viewport rectangle.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="viewportRectangle">The viewport rectangle in scaled coordinate space.</param>
    /// <param name="scale">Scale factor.</param>
    /// <returns><see langword="true"/> if the page intersects with the viewport; otherwise, <see langword="false"/>.</returns>
    public static bool IsPageVisible(this PdfPanelPage page, SKRect viewportRectangle, float scale)
    {
        if (page == null)
        {
            throw new System.ArgumentNullException(nameof(page));
        }

        SKRect pageBounds = page.GetScaledPageBounds(scale);
        return viewportRectangle.IntersectsWith(pageBounds);
    }

    //internal static SKPoint ToPagePosition(this PdfPanelPage page, SKPoint canvasPosition, float scale, bool cropToMargins = false)
    //{
    //    SKPoint scaledPosition = new SKPoint(canvasPosition.X / scale, canvasPosition.Y / scale);
    //    SKPoint result = new SKPoint(scaledPosition.X - page.Offset.X, scaledPosition.Y - page.Offset.Y);

    //    if (cropToMargins)
    //    {
    //        if (result.X < 0)
    //        {
    //            result.X = 0;
    //        }

    //        if (result.Y < 0)
    //        {
    //            result.Y = 0;
    //        }

    //        SKSize rotatedSize = page.GetRotatedSize();

    //        if (result.X > rotatedSize.Width)
    //        {
    //            result.X = rotatedSize.Width;
    //        }

    //        if (result.Y > rotatedSize.Height)
    //        {
    //            result.Y = rotatedSize.Height;
    //        }
    //    }

    //    return result;
    //}

    //internal static SKPoint ToCanvasPosition(this PdfPanelPage page, SKPoint pagePosition, float scale)
    //{
    //    return new SKPoint((pagePosition.X + page.Offset.X) * scale, (pagePosition.Y + page.Offset.Y) * scale);
    //}


    // TODO: rework below to create some "TO PAGE" matrix
    ///// <summary>
    ///// Returns the matrix that represents the rotation of the page.
    ///// </summary>
    ///// <param name="width">Page width.</param>
    ///// <param name="height">Page height.</param>
    ///// <param name="rotation">Rotation in degrees.</param>
    ///// <returns></returns>
    //public static SKMatrix GetPageRotationMatrix(double width, double height, int rotation)
    //{
    //    rotation = rotation % 360;

    //    if (rotation < 0)
    //    {
    //        rotation += 360;
    //    }

    //    Matrix matrix = new Matrix();
    //    matrix.Rotate(-rotation);

    //    switch (rotation)
    //    {
    //        case 90:
    //            matrix.Translate(0, height);
    //            break;
    //        case 180:
    //            matrix.Translate(width, height);
    //            break;
    //        case 270:
    //            matrix.Translate(width, 0);
    //            break;
    //    }

    //    return matrix;
    //}
}

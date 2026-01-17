using SkiaSharp;

namespace PdfRender.View;

/// <summary>
/// Represents the information about a visible page.
/// </summary>
public readonly struct VisiblePageInfo
{
    public VisiblePageInfo(int pageNumber, SKPoint offset, PageInfo pageInfo, int userRotation)
    {
        PageNumber = pageNumber;
        Offset = offset;
        PageInfo = pageInfo;

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
    public PageInfo PageInfo { get; }

    /// <summary>
    /// Gets the user rotation of the page.
    /// </summary>
    public int UserRotation { get; }

    /// <summary>
    /// Gets the total rotation of the page.
    /// </summary>
    public int TotalRotation => PageInfo.GetTotalRotation(UserRotation);

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public SKSize RotatedSize => PageInfo.GetRotatedSize(UserRotation);

    /// <summary>
    /// Gets the unscaled bounds of the page.
    /// </summary>
    public SKRect Bounds => SKRect.Create(Offset, RotatedSize);

    /// <summary>
    /// Gets the scaled bounds of the page.
    /// </summary>
    /// <param name="scale">Scale factor.</param>
    /// <returns></returns>
    public SKRect GetScaledBounds(float scale) => SKRect.Create(Offset.X * scale, Offset.Y * scale, RotatedSize.Width * scale, RotatedSize.Height * scale);

    /// <summary>
    /// Converts the canvas position to the page position.
    /// </summary>
    /// <param name="canvasPosition">Position on canvas.</param>
    /// <param name="scale">Scale factor.</param>
    /// <param name="cropToMargins">If position will be cropped to page bounds.</param>
    /// <returns>Position on page.</returns>
    public SKPoint ToPagePosition(SKPoint canvasPosition, float scale, bool cropToMargins = false)
    {
        SKPoint scaledPosition = new SKPoint(canvasPosition.X / scale, canvasPosition.Y / scale);
        var result = new SKPoint(scaledPosition.X - Offset.X, scaledPosition.Y - Offset.Y);

        if (cropToMargins)
        {
            if (result.X < 0)
            {
                result.X = 0;
            }
            if (result.Y < 0)
            {
                result.Y = 0;
            }
            if (result.X > RotatedSize.Width)
            {
                result.X = RotatedSize.Width;
            }
            if (result.Y > RotatedSize.Height)
            {
                result.Y = RotatedSize.Height;
            }
        }

        return result;
    }

    /// <summary>
    /// Converts the page position to the canvas position.
    /// </summary>
    /// <param name="pagePosition">Page position.</param>
    /// <param name="scale">Scale factor.</param>
    /// <returns>Position on canvas.</returns>
    public SKPoint ToCanvasPosition(SKPoint pagePosition, float scale)
    {
        return new SKPoint((pagePosition.X + Offset.X) * scale, (pagePosition.Y + Offset.Y) * scale);
    }

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
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;

namespace PdfPixel.PdfPanel;

internal readonly struct VisiblePageInfo
{
    public VisiblePageInfo(int pageNumber, SKPoint offset, PageInfo pageInfo, int userRotation)
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
    public PageInfo Info { get; }

    /// <summary>
    /// Gets the user rotation of the page.
    /// </summary>
    public int UserRotation { get; }

    /// <summary>
    /// Gets the total rotation of the page.
    /// </summary>
    public int TotalRotation => Info.GetTotalRotation(UserRotation);

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public SKSize RotatedSize => Info.GetRotatedSize(UserRotation);

    /// <summary>
    /// Gets the unscaled bounds of the page.
    /// </summary>
    public SKRect Bounds => SKRect.Create(Offset, RotatedSize);

    /// <summary>
    /// Gets the scaled bounds of the page.
    /// </summary>
    /// <param name="scale">Scale factor.</param>
    /// <returns></returns>
    internal SKRect GetScaledBounds(double scale) => SKRect.Create((float)(Offset.X * scale), (float)(Offset.Y * scale), (float)(RotatedSize.Width * scale), (float)(RotatedSize.Height * scale));

    ///// <summary>
    ///// Converts the canvas position to the page position.
    ///// </summary>
    ///// <param name="canvasPosition">Position on canvas.</param>
    ///// <param name="scale">Scale factor.</param>
    ///// <param name="cropToMargins">If position will be cropped to page bounds.</param>
    ///// <returns>Position on page.</returns>
    //public Point ToPagePosition(Point canvasPosition, double scale, bool cropToMargins = false)
    //{
    //    SKPoint scaledPosition = new Point(canvasPosition.X / scale, canvasPosition.Y / scale);
    //    var result = new Point(scaledPosition.X - Offset.X, scaledPosition.Y - Offset.Y);

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
    //        if (result.X > RotatedSize.Width)
    //        {
    //            result.X = RotatedSize.Width;
    //        }
    //        if (result.Y > RotatedSize.Height)
    //        {
    //            result.Y = RotatedSize.Height;
    //        }
    //    }

    //    return result;
    //}

    ///// <summary>
    ///// Converts the page position to the canvas position.
    ///// </summary>
    ///// <param name="pagePosition">Page position.</param>
    ///// <param name="scale">Scale factor.</param>
    ///// <returns>Position on canvas.</returns>
    //public Point ToCanvasPosition(Point pagePosition, double scale)
    //{
    //    return new Point((pagePosition.X + Offset.X) * scale, (pagePosition.Y + Offset.Y) * scale);
    //}

    ///// <summary>
    ///// Returns the matrix that represents the rotation of the page.
    ///// </summary>
    ///// <param name="width">Page width.</param>
    ///// <param name="height">Page height.</param>
    ///// <param name="rotation">Rotation in degrees.</param>
    ///// <returns></returns>
    //public static Matrix GetPageRotationMatrix(double width, double height, int rotation)
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

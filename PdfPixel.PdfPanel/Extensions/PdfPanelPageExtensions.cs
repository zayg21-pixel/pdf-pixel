using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Extensions;

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

    /// <summary>
    /// Determines whether a point in page coordinates is within the page bounds.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="pagePoint">Point in page coordinate space (unrotated).</param>
    /// <returns><see langword="true"/> if the point is within the page bounds; otherwise, <see langword="false"/>.</returns>
    public static bool IsPointInPageBounds(this PdfPanelPage page, SKPoint pagePoint)
    {
        if (page == null)
        {
            throw new System.ArgumentNullException(nameof(page));
        }

        return pagePoint.X >= 0 && pagePoint.X <= page.Info.Width &&
               pagePoint.Y >= 0 && pagePoint.Y <= page.Info.Height;
    }

    /// <summary>
    /// Gets the transformation matrix from viewport coordinates to page coordinates.
    /// Transforms from viewport space (with scroll, scale, offset, and rotation) to unrotated page space.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="context">The panel context providing viewport and scroll information.</param>
    /// <returns>Matrix that transforms viewport coordinates to page coordinates.</returns>
    public static SKMatrix ViewportToPageMatrix(this PdfPanelPage page, PdfPanelContext context)
    {
        if (context == null)
        {
            throw new System.ArgumentNullException(nameof(context));
        }

        return page.ViewportToPageMatrix(context.Scale, context.HorizontalOffset, context.VerticalOffset);
    }

    /// <summary>
    /// Gets the transformation matrix from viewport coordinates to page coordinates.
    /// Transforms from viewport space (with scroll, scale, offset, and rotation) to unrotated page space.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="scale">Scale factor.</param>
    /// <param name="horizontalOffset">Horizontal scroll offset.</param>
    /// <param name="verticalOffset">Vertical scroll offset.</param>
    /// <returns>Matrix that transforms viewport coordinates to page coordinates.</returns>
    public static SKMatrix ViewportToPageMatrix(this PdfPanelPage page, float scale, float horizontalOffset, float verticalOffset)
    {
        if (page == null)
        {
            throw new System.ArgumentNullException(nameof(page));
        }

        var matrix = SKMatrix.Identity;

        // Step 1: Add scroll offset to reverse viewport translation
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(horizontalOffset, verticalOffset));

        // Step 2: Subtract page offset (in scaled space) to get to page origin
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-page.Offset.X, -page.Offset.Y));

        // Step 3: Apply inverse scale to get to unscaled page space
        matrix = matrix.PostConcat(SKMatrix.CreateScale(1f / scale, 1f / scale));

        // Step 4: Apply inverse rotation to get to unrotated page space
        int totalRotation = page.GetTotalRotation();
        if (totalRotation != 0)
        {
            SKSize unrotatedSize = new SKSize(page.Info.Width, page.Info.Height);
            matrix = matrix.PostConcat(GetInverseRotationMatrix(unrotatedSize.Width, unrotatedSize.Height, totalRotation));
        }

        return matrix;
    }

    /// <summary>
    /// Calculates the inverse rotation transformation matrix for a rectangular area with the specified width, height,
    /// and rotation angle.
    /// </summary>
    /// <param name="width">The width of the area to which the rotation is applied. Must be a positive value.</param>
    /// <param name="height">The height of the area to which the rotation is applied. Must be a positive value.</param>
    /// <param name="rotationDegrees">The rotation angle, in degrees, to invert. Valid values are between 0 and 359; negative values are normalized to
    /// this range.</param>
    /// <returns>An SKMatrix representing the inverse rotation transformation for the specified area and angle.</returns>
    public static SKMatrix GetInverseRotationMatrix(float width, float height, int rotationDegrees)
    {
        rotationDegrees = rotationDegrees % 360;
        if (rotationDegrees < 0)
        {
            rotationDegrees += 360;
        }

        var matrix = SKMatrix.Identity;

        switch (rotationDegrees)
        {
            case 90:
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-height, 0));
                matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(-90));
                break;
            case 180:
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-width, -height));
                matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(-180));
                break;
            case 270:
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(0, -width));
                matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(-270));
                break;
        }

        return matrix;
    }

    /// <summary>
    /// Converts a point from PDF coordinates (bottom-left origin, Y-up) to page coordinates (top-left origin, Y-down).
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="pdfPoint">Point in PDF coordinates.</param>
    /// <returns>Point in page coordinates.</returns>
    public static SKPoint FromPdfPoint(this PdfPanelPage page, SKPoint pdfPoint)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        return new SKPoint(
            pdfPoint.X,
            page.Info.Height - pdfPoint.Y);
    }

    /// <summary>
    /// Converts a rectangle from PDF coordinates (bottom-left origin, Y-up) to page coordinates (top-left origin, Y-down).
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="pdfRect">Rectangle in PDF coordinates.</param>
    /// <returns>Rectangle in page coordinates.</returns>
    public static SKRect FromPdfRect(this PdfPanelPage page, SKRect pdfRect)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        return SKRect.Create(
            pdfRect.Left,
            page.Info.Height - pdfRect.Bottom,
            pdfRect.Width,
            pdfRect.Height);
    }
}


using SkiaSharp;

namespace PdfRender.Canvas
{
    internal static class PdfViewerPageExtensions
    {
        internal static int GetTotalRotation(this PdfViewerPage page)
        {
            return page.Info.GetTotalRotation(page.UserRotation);
        }

        internal static SKSize GetRotatedSize(this PdfViewerPage page)
        {
            return page.Info.GetRotatedSize(page.UserRotation);
        }

        internal static SKRect GetBounds(this PdfViewerPage page)
        {
            SKSize rotatedSize = page.GetRotatedSize();
            return SKRect.Create(page.Offset, rotatedSize);
        }

        internal static SKRect GetScaledBounds(this PdfViewerPage page, float scale)
        {
            SKSize rotatedSize = page.GetRotatedSize();
            return SKRect.Create(page.Offset.X * scale, page.Offset.Y * scale, rotatedSize.Width * scale, rotatedSize.Height * scale);
        }

        internal static SKPoint ToPagePosition(this PdfViewerPage page, SKPoint canvasPosition, float scale, bool cropToMargins = false)
        {
            SKPoint scaledPosition = new SKPoint(canvasPosition.X / scale, canvasPosition.Y / scale);
            SKPoint result = new SKPoint(scaledPosition.X - page.Offset.X, scaledPosition.Y - page.Offset.Y);

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

                SKSize rotatedSize = page.GetRotatedSize();

                if (result.X > rotatedSize.Width)
                {
                    result.X = rotatedSize.Width;
                }

                if (result.Y > rotatedSize.Height)
                {
                    result.Y = rotatedSize.Height;
                }
            }

            return result;
        }

        internal static SKPoint ToCanvasPosition(this PdfViewerPage page, SKPoint pagePosition, float scale)
        {
            return new SKPoint((pagePosition.X + page.Offset.X) * scale, (pagePosition.Y + page.Offset.Y) * scale);
        }

        internal static bool IsVisible(this PdfViewerPage page, float offset, float canvasHeight)
        {
            var pageHeight = page.Info.GetRotatedSize(page.UserRotation).Height;
            var pageTop = offset;
            var pageBottom = offset + pageHeight;

            return (pageTop >= 0 && pageTop <= canvasHeight) || (pageBottom >= 0 && pageBottom <= canvasHeight) || (pageTop <= 0 && pageBottom >= canvasHeight);
        }

        internal static bool IsCurrent(this PdfViewerPage page, float offset, float pageGap, float canvasHeight)
        {
            var pageHeight = page.Info.GetRotatedSize(page.UserRotation).Height;
            var pageTop = offset;
            var pageBottom = offset + pageHeight + pageGap;

            return (pageTop >= -pageGap && pageTop <= canvasHeight / 2) || (pageTop <= -pageGap && pageBottom >= canvasHeight / 2);
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
}

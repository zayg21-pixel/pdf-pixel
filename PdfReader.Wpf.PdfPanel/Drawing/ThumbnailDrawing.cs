using SkiaSharp;
using System;

namespace PdfReader.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Creates a thumbnail image from a <see cref="SKPicture"/>.
    /// </summary>
    internal static class ThumbnailDrawing
    {
        public static SKImage GetThumbnail(SKPicture picture, int maxThumbnailSize, PageInfo page, int userRotation)
        {
            var maxDimension = Math.Max(picture.CullRect.Width, picture.CullRect.Height);

            var scale = maxThumbnailSize / maxDimension;

            var thumbnailSize = new SKSizeI((int)(picture.CullRect.Width * scale), (int)(picture.CullRect.Height * scale));

            var totalRotation = page.GetTotalRotation(userRotation);
            var resultSize = totalRotation % 180 == 0 ? thumbnailSize : new SKSizeI(thumbnailSize.Height, thumbnailSize.Width);

            var transform = SkCanvasExtensions.GetPictureTransformMatrix(picture.CullRect.Width, picture.CullRect.Height, new PageInfo(thumbnailSize.Width, thumbnailSize.Height, totalRotation), 0);

            return SKImage.FromPicture(picture, resultSize, transform);
        }
    }
}

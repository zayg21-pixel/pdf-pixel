using SkiaSharp;
using System;
using System.Linq;

namespace PdfReader.Wpf.PdfPanel.Drawing
{
    [Flags]
    internal enum PageDrawFlags
    {
        Content = 1,
        Shadow = 2,
        Background = 4
    }

    /// <summary>
    /// Extensions for drawing PDF data on <see cref="SKCanvas"/>.
    /// </summary>
    internal static class SkCanvasExtensions
    {
        public static void Clear(this SKCanvas canvas, System.Windows.Media.Color color)
        {
            canvas.Clear(new SKColor(color.R, color.G, color.B, color.A));
        }

        public static void DrawPageFromRequest(this SKCanvas canvas, int pageNumber, PagesDrawingRequest request, PageDrawFlags drawFlags)
        {
            if (!request.VisiblePages.Any(x => x.PageNumber == pageNumber))
            {
                return;
            }

            var page = request.VisiblePages.First(x => x.PageNumber == pageNumber);

            int savedPageCount = canvas.Save();
            canvas.Scale((float)request.Scale, (float)request.Scale);
            canvas.Translate((float)page.Offset.X, (float)page.Offset.Y);

            if (drawFlags.HasFlag(PageDrawFlags.Shadow))
            {
                DrawPageShadow(canvas, page);
            }

            if (drawFlags.HasFlag(PageDrawFlags.Background))
            {
                DrawPageBackground(canvas, page);
            }

            if (!request.Pages.TryGetPictureFromCache(page.PageNumber, out var picture))
            {
                canvas.RestoreToCount(savedPageCount);
                return;
            }

            if (drawFlags.HasFlag(PageDrawFlags.Content))
            {
                DrawCachedPicture(canvas, picture, page);
            }

            canvas.RestoreToCount(savedPageCount);
        }

        private static void DrawCachedPicture(SKCanvas canvas, CachedSkPicture picture, VisiblePageInfo page)
        {
            lock (picture.DisposeLocker)
            {
                if (picture.IsDisposed || picture.Picture == null)
                {
                    return;
                }

                var transformMatrix = GetPictureTransformMatrix(picture.Picture.CullRect.Width, picture.Picture.CullRect.Height, page.PageInfo, page.UserRotation);
                canvas.DrawPicture(picture.Picture, in transformMatrix);
            }
        }

        public static SKMatrix GetPictureTransformMatrix(float pictureWidth, float pictureHeight, PageInfo pageInfo, int userRotation)
        {
            var rotation = pageInfo.GetTotalRotation(userRotation);
            var infoWidth = pageInfo.Width;
            var infoHeight = pageInfo.Height;

            var matrixScale = SKMatrix.CreateScale((float)(infoWidth / pictureWidth), (float)(infoHeight / pictureHeight));
            var matrixRotation = SKMatrix.CreateRotationDegrees(rotation);

            var translateX = rotation switch
            {
                180 => -infoWidth,
                270 => -infoWidth,
                _ => 0
            };
            var translateY = rotation switch
            {
                90 => -infoHeight,
                180 => -infoHeight,
                _ => 0
            };

            var matrixTranslation = SKMatrix.CreateTranslation((float)translateX, (float)translateY);

            return SKMatrix.Concat(SKMatrix.Concat(matrixRotation, matrixTranslation), matrixScale);
        }

        private static void DrawPageShadow(SKCanvas canvas, VisiblePageInfo pageInfo)
        {
            var pageRectangle = new SKRect(0, 0, (float)pageInfo.RotatedSize.Width, (float)pageInfo.RotatedSize.Height);

            if (!pageRectangle.Contains(canvas.LocalClipBounds))
            {
                using var filter = SKImageFilter.CreateDropShadowOnly(0f, 0f, 3f, 3f, SKColors.Gray);
                using var shadowPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    ImageFilter = filter
                };

                canvas.DrawRect(pageRectangle, shadowPaint);
            }
        }

        private static void DrawPageBackground(SKCanvas canvas, VisiblePageInfo pageInfo)
        {
            var pageRectangle = new SKRect(0, 0, (float)pageInfo.RotatedSize.Width, (float)pageInfo.RotatedSize.Height);

            using var backgroundFill = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.White
            };

            canvas.DrawRect(pageRectangle, backgroundFill);
        }
    }
}

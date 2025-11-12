using PdfReader.Wpf.PdfPanel.Rendering;
using SkiaSharp;
using System;

namespace PdfReader.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Contains a cached <see cref="SKPicture"/> and its thumbnail.
    /// </summary>
    internal sealed class CachedSkPicture : IDisposable
    {
        public CachedSkPicture(SKPicture picture, SKImage thumbnail, int pageNumber, double scale, PageGraphicsInfo graphicsInfo)
        {
            Picture = picture;
            Thumbnail = thumbnail;
            PageNumber = pageNumber;
            Scale = scale;
            GraphicsInfo = graphicsInfo;
        }

        public SKPicture Picture { get; }

        public SKImage Thumbnail { get; }

        public int PageNumber { get; }

        public double Scale { get; }

        public PageGraphicsInfo GraphicsInfo { get; }

        public bool ShouldUpdateCache(double requiredScale)
        {
            if (Scale == requiredScale)
            {
                return false;
            }
            if (!GraphicsInfo.HasImages)
            {
                return false;
            }
            else
            {
                return !(Scale >= GraphicsInfo.MaxImageScale && requiredScale >= GraphicsInfo.MaxImageScale);
            }
        }

        public object DisposeLocker { get; } = new object();

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            lock (DisposeLocker)
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                Picture.Dispose();
                Thumbnail?.Dispose();
            }
        }
    }
}

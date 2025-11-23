using SkiaSharp;
using System;
using System.Collections.Concurrent;
using PdfReader.Models;

namespace PdfReader.Wpf.PdfPanel.Rendering
{
    /// <summary>
    /// Implements the IPdfRenderer interface.
    /// </summary>
    internal sealed class PdfRenderer : IPdfRenderer
    {
        private readonly PdfDocument document;
        private readonly ConcurrentDictionary<int, PageGraphicsInfo> pageGraphicsInfoDictionary = new ConcurrentDictionary<int, PageGraphicsInfo>();

        public PdfRenderer(PdfDocument document)
        {
            this.document = document;
        }

        public PageInfo GetPageInfo(int pageNumber)
        {
            var pdfPage = document.Pages[pageNumber - 1];
            return new PageInfo(pdfPage.CropBox.Width, pdfPage.CropBox.Height, pdfPage.Rotation);
        }

        public AnnotationPopup[] GetAnnotationPopups(int pageNumber)
        {
            return Array.Empty<AnnotationPopup>();
        }

        public PageGraphicsInfo GetPageGraphicsInfo(int pageNumber)
        {
            if (pageGraphicsInfoDictionary.TryGetValue(pageNumber, out var pageInfo))
            {
                return pageInfo;
            }
            else
            {
                bool hasImages = true; // TODO: estimate if the page has images
                var info = new PageGraphicsInfo(hasImages);

                pageGraphicsInfoDictionary.TryAdd(pageNumber, info);

                return info;
            }
        }

        public SKPicture GetPicture(int pageNumber, double scale)
        {
            try
            {
                return GetPictureInternal(pageNumber, scale);
            }
            catch
            {
                return null;
            }
        }

        private SKPicture GetPictureInternal(int pageNumber, double scale)
        {
            var pdfPage = document.Pages[pageNumber - 1];
            var pageInfo = GetPageGraphicsInfo(pageNumber);

            using var recorder = new SKPictureRecorder();
            using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            pdfPage.Draw(canvas);

            canvas.Flush();
            return recorder.EndRecording();
        }

        public void Dispose()
        {
            document.Dispose();
        }
    }
}

using PdfReader.Models;
using SkiaSharp;
using System;
using System.IO;
using System.Windows.Shapes;

namespace PdfReader.Wpf.PdfPanel.Rendering
{
    /// <summary>
    /// Implements the IPdfRenderer interface.
    /// </summary>
    internal sealed class PdfRenderer : IPdfRenderer
    {
        private readonly PdfDocument document;

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

        public SKPicture GetPicture(int pageNumber, double scale)
        {
            try
            {
                return GetPictureInternal(pageNumber, scale, previewMode: false);
            }
            catch
            {
                return null;
            }
        }

        public SKImage GetThumbnail(int pageNumber, int maxThumbnailSize)
        {
            try
            {
                var pdfPage = document.Pages[pageNumber - 1];
                var maxDimension = Math.Max(pdfPage.CropBox.Width, pdfPage.CropBox.Height);
                var scale = maxThumbnailSize / maxDimension;

                var width = (int)Math.Max(1, Math.Round(pdfPage.CropBox.Width * scale));
                var height = (int)Math.Max(1, Math.Round(pdfPage.CropBox.Height * scale));

                var bitmapInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var bitmap = new SKBitmap(bitmapInfo);

                using var canvas = new SKCanvas(bitmap);

                canvas.Clear(SKColors.Transparent);
                canvas.Scale((float)scale);
                canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

                var parameters = new PdfRenderingParameters
                {
                    ScaleFactor = (float)scale,
                    PreviewMode = true
                };

                pdfPage.Draw(canvas, parameters);
                canvas.Flush();

                return SKImage.FromBitmap(bitmap);
            }
            catch
            {
                return null;
            }
        }

        private SKPicture GetPictureInternal(int pageNumber, double scale, bool previewMode)
        {
            var pdfPage = document.Pages[pageNumber - 1];

            using var recorder = new SKPictureRecorder();
            using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            var parameters = new PdfRenderingParameters { ScaleFactor = (float)scale, PreviewMode = false };
            pdfPage.Draw(canvas, parameters);

            // TODO: [HIGH] move to view model of Demo app
            //var chars = pdfPage.ExtractText();
            //var chunker = new TextExtraction.PdfTextChunker();
            //var words = chunker.ChunkCharacters(chars);
            //var paint = new SKPaint
            //{
            //    Style = SKPaintStyle.Stroke,
            //    Color = SKColors.Red,
            //    StrokeWidth = 0.5f,
            //    IsAntialias = true
            //};

            //foreach (var word in words)
            //{
            //    canvas.DrawRect(word.BoundingBox, paint);
            //}

            //foreach (var c in chars)
            //{
            //    canvas.DrawRect(c.BoundingBox, paint);
            //}

            canvas.Flush();
            var picture = recorder.EndRecording();

            //string path = $"page_{pageNumber}.skp";
            //using var fileStream = File.OpenWrite(path);
            //picture.Serialize(fileStream);

            return picture;
        }

        public void Dispose()
        {
            document.Dispose();
        }
    }
}

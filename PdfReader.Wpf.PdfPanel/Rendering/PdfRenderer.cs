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

            using var recorder = new SKPictureRecorder();
            using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            pdfPage.Draw(canvas);

            // TODO: move to view model of Demo app
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

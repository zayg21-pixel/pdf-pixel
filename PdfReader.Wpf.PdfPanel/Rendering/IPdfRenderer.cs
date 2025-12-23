using SkiaSharp;
using System;

namespace PdfReader.Wpf.PdfPanel.Rendering
{
    /// <summary>
    /// Represents a renderer that can render PDF documents to Skia graphics
    /// and provides information about the pages and annotations in the document.
    /// </summary>
    internal interface IPdfRenderer : IDisposable
    {
        PageInfo GetPageInfo(int pageNumber);

        SKPicture GetPicture(int pageNumber, double scale);

        SKPicture GetThumbnailPicture(int pageNumber, int maxThumbnailSize);

        AnnotationPopup[] GetAnnotationPopups(int pageNumber);
    }
}
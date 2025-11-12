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
        PageGraphicsInfo GetPageGraphicsInfo(int pageNumber);

        PageInfo GetPageInfo(int pageNumber);

        SKPicture GetPicture(int pageNumber, double scale);

        AnnotationPopup[] GetAnnotationPopups(int pageNumber);
    }
}
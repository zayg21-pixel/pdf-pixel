using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Interface for image drawing implementations
    /// Handles complete image processing from PDF data to rendered output
    /// </summary>
    public interface IImageDrawer
    {
        /// <summary>
        /// Draw a PDF image with the specified graphics state
        /// Handles all image processing including color space conversion, masking, and filtering
        /// </summary>
        void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, PdfPage page, SKRect destRect);
    }
}
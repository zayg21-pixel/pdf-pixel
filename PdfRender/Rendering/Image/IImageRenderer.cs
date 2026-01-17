using SkiaSharp;
using PdfRender.Imaging.Model;
using PdfRender.Rendering.State;

namespace PdfRender.Rendering.Image;

/// <summary>
/// Interface for image drawing implementations
/// Handles complete image processing from PDF data to rendered output
/// </summary>
public interface IImageRenderer
{
    /// <summary>
    /// Draw a PDF image with the specified graphics state
    /// Handles all image processing including color space conversion, masking, and filtering
    /// </summary>
    void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state);
}
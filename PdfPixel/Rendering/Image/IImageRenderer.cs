using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering.Image;

/// <summary>
/// Interface for image drawing implementations.
/// Handles complete image processing from PDF data to rendered output.
/// </summary>
public interface IImageRenderer
{
    /// <summary>
    /// Draw a PDF image with the specified graphics state.
    /// Handles all image processing including color space conversion, masking, and filtering.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    /// <param name="pdfImage">The PDF image to render.</param>
    /// <param name="state">The current graphics state for rendering.</param>
    void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state);
}
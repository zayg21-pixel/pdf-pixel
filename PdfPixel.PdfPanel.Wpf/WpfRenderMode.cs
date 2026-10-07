namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Specifies the rendering backend of <see cref="WpfPdfPanel"/>.
/// </summary>
public enum WpfRenderMode
{
    /// <summary>
    /// CPU-only rendering via SkiaSharp raster surfaces, presented through a WriteableBitmap.
    /// </summary>
    Software,

    /// <summary>
    /// GPU-accelerated rendering via OpenGL (WGL), with pixel readback to a WriteableBitmap.
    /// </summary>
    OpenGl
}
